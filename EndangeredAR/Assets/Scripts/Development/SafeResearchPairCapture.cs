#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Text;
using EndangeredAR.AI;
using UnityEngine;

namespace EndangeredAR.Development
{
    internal enum SafePairCaptureFailure
    {
        None,
        NoCurrentCompletion,
        InvalidCompletion,
        StaleCompletion,
        ValidationFailed,
        UnapprovedPrompt,
        EmptyReply,
        UntrustedSource
    }

    internal sealed class SafeResearchPairSnapshot
    {
        internal SafeResearchPairSnapshot(
            int completionId,
            string promptId,
            string userMessage,
            string assistantReply,
            string finalSource,
            string answerMode,
            string contentAuthority)
        {
            CompletionId = completionId;
            PromptId = promptId;
            UserMessage = userMessage;
            AssistantReply = assistantReply;
            FinalSource = finalSource;
            AnswerMode = answerMode;
            ContentAuthority = contentAuthority;
        }

        public int CompletionId { get; }
        public string PromptId { get; }
        public string UserMessage { get; }
        public string AssistantReply { get; }
        public string FinalSource { get; }
        public string AnswerMode { get; }
        public string ContentAuthority { get; }
        public string ValidationResult => "passed";
    }

    internal static class SafeResearchPairCapture
    {
        private static int activeCompletionId;
        private static bool hasActiveCompletion;
        private static SafeResearchPairSnapshot latestAccepted;

        public static SafePairCaptureFailure LastFailure { get; private set; }

        public static void BeginCompletion(int completionId)
        {
            activeCompletionId = completionId;
            hasActiveCompletion = completionId > 0;
            latestAccepted = null;
            LastFailure = hasActiveCompletion
                ? SafePairCaptureFailure.None
                : SafePairCaptureFailure.InvalidCompletion;
        }

        public static bool TryRecordAccepted(
            int completionId,
            string userMessage,
            string assistantReply,
            AIResponse response,
            bool responseValidationPassed)
        {
            if (!hasActiveCompletion || completionId != activeCompletionId)
            {
                LastFailure = SafePairCaptureFailure.StaleCompletion;
                return false;
            }

            latestAccepted = null;
            if (!responseValidationPassed)
            {
                LastFailure = SafePairCaptureFailure.ValidationFailed;
                return false;
            }

            if (!ApprovedResearchPromptRegistry.TryResolve(userMessage, out var promptId))
            {
                LastFailure = SafePairCaptureFailure.UnapprovedPrompt;
                return false;
            }

            var reply = assistantReply?.Trim();
            if (string.IsNullOrEmpty(reply))
            {
                LastFailure = SafePairCaptureFailure.EmptyReply;
                return false;
            }

            if (response == null ||
                !AIFinalSourceProtocol.TryParseExact(response.source, out var finalSource) ||
                finalSource != AIFinalSource.OnDeviceLlm ||
                response.LanguageGenerator != LanguageGenerator.OnDeviceLlm ||
                string.Equals(response.answerMode, "system_status", StringComparison.Ordinal))
            {
                LastFailure = SafePairCaptureFailure.UntrustedSource;
                return false;
            }

            latestAccepted = new SafeResearchPairSnapshot(
                completionId,
                promptId,
                NormalizeUserMessage(userMessage),
                reply,
                AIFinalSourceProtocol.ToWireValue(finalSource),
                CleanCode(response.answerMode),
                ContentAuthorityProtocol.ToWireValue(response.ContentAuthority));
            LastFailure = SafePairCaptureFailure.None;
            return true;
        }

        public static bool TryCaptureCurrent(out SafeResearchPairSnapshot snapshot)
        {
            snapshot = latestAccepted;
            if (snapshot != null)
            {
                LastFailure = SafePairCaptureFailure.None;
                return true;
            }

            if (LastFailure == SafePairCaptureFailure.None)
            {
                LastFailure = SafePairCaptureFailure.NoCurrentCompletion;
            }
            return false;
        }

        public static void Invalidate(int completionId)
        {
            if (!hasActiveCompletion || completionId != activeCompletionId)
            {
                return;
            }

            hasActiveCompletion = false;
            latestAccepted = null;
            LastFailure = SafePairCaptureFailure.NoCurrentCompletion;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        internal static void Reset()
        {
            activeCompletionId = 0;
            hasActiveCompletion = false;
            latestAccepted = null;
            LastFailure = SafePairCaptureFailure.None;
        }

        private static string NormalizeUserMessage(string value)
        {
            return (value ?? string.Empty)
                .Normalize(NormalizationForm.FormKC)
                .Trim();
        }

        private static string CleanCode(string value)
        {
            if (string.IsNullOrEmpty(value) || value.Length > 64)
            {
                return "unknown";
            }

            foreach (var character in value)
            {
                if (!(character == '_' ||
                      (character >= 'a' && character <= 'z') ||
                      (character >= '0' && character <= '9')))
                {
                    return "unknown";
                }
            }
            return value;
        }
    }

    internal static class ApprovedResearchPromptRegistry
    {
        public static bool TryResolve(string userMessage, out string promptId)
        {
            switch ((userMessage ?? string.Empty).Normalize(NormalizationForm.FormKC).Trim())
            {
                case "你好":
                    promptId = "r34a4_greeting_hello";
                    return true;
                case "我今天有点累":
                    promptId = "r34a4_social_tired";
                    return true;
                case "你的学名是什么？":
                case "你的学名是什么?":
                    promptId = "r34a4_science_name";
                    return true;
                default:
                    promptId = string.Empty;
                    return false;
            }
        }
    }
}
#endif
