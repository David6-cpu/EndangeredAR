#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections.Generic;
using EndangeredAR.AI;
using UnityEngine;

namespace EndangeredAR.Development
{
    public sealed class AIResponseProvenanceSnapshot
    {
        private readonly string[] providerAttempts;

        internal AIResponseProvenanceSnapshot(
            AIFinalSource finalSource,
            string answerMode,
            string routeMode,
            string contentAuthority,
            string languageGenerator,
            string[] providerAttempts,
            string groundingTopic,
            string memoryMentionPolicy,
            string memoryStatus,
            bool fallbackUsed,
            string fallbackReasonCode,
            string errorCode,
            long elapsedMilliseconds)
        {
            FinalSource = finalSource;
            AnswerMode = answerMode;
            RouteMode = routeMode;
            ContentAuthority = contentAuthority;
            LanguageGenerator = languageGenerator;
            this.providerAttempts = providerAttempts == null
                ? Array.Empty<string>()
                : (string[])providerAttempts.Clone();
            GroundingTopic = groundingTopic;
            MemoryMentionPolicy = memoryMentionPolicy;
            MemoryStatus = memoryStatus;
            FallbackUsed = fallbackUsed;
            FallbackReasonCode = fallbackReasonCode;
            ErrorCode = errorCode;
            ElapsedMilliseconds = Math.Max(0L, elapsedMilliseconds);
        }

        public AIFinalSource FinalSource { get; }
        public string FinalSourceWireValue => AIFinalSourceProtocol.ToWireValue(FinalSource);
        public string AnswerMode { get; }
        public string RouteMode { get; }
        public string ContentAuthority { get; }
        public string LanguageGenerator { get; }
        public IReadOnlyList<string> ProviderAttempts => Array.AsReadOnly((string[])providerAttempts.Clone());
        public string GroundingTopic { get; }
        public string MemoryMentionPolicy { get; }
        public string MemoryStatus { get; }
        public bool FallbackUsed { get; }
        public string FallbackReasonCode { get; }
        public string ErrorCode { get; }
        public long ElapsedMilliseconds { get; }
    }

    public static class AIResponseProvenanceRecorder
    {
        public static AIResponseProvenanceSnapshot Latest { get; private set; }

        public static bool TryRecord(AIResponse response)
        {
            if (response == null || !AIFinalSourceProtocol.TryParseExact(response.source, out var finalSource))
            {
                Latest = null;
                return false;
            }

            Latest = new AIResponseProvenanceSnapshot(
                finalSource,
                CleanCode(response.answerMode, "unknown"),
                ResolveRouteMode(response),
                ContentAuthorityProtocol.ToWireValue(response.ContentAuthority),
                LanguageGeneratorProtocol.ToWireValue(response.LanguageGenerator),
                CopyKnownAttempts(response.ProviderAttempts),
                response.GroundingTopic == GroundingTopic.Diet ? "diet" : "none",
                MemoryMentionModeProtocol.ToWireValue(response.MemoryMentionMode),
                CleanMemoryStatus(response.ProvenanceMemoryStatus),
                response.FallbackUsed,
                AIProvenanceProtocol.SanitizeReasonCode(response.FallbackReasonCode),
                AIProvenanceProtocol.SanitizeReasonCode(response.ProvenanceErrorCode),
                response.ElapsedMilliseconds);
            return true;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        internal static void Reset()
        {
            Latest = null;
        }

        private static string ResolveRouteMode(AIResponse response)
        {
            if (response.ProvenanceRouteMode == "deterministic")
            {
                return "deterministic";
            }

            switch (response.RouteMode)
            {
                case AIRouteMode.LocalOnly:
                    return "local_only";
                case AIRouteMode.LocalFirstCloudFallback:
                    return "local_first_cloud_fallback";
                case AIRouteMode.CloudOnly:
                default:
                    return "cloud_only";
            }
        }

        private static string[] CopyKnownAttempts(string[] values)
        {
            var result = new List<string>();
            foreach (var value in values ?? Array.Empty<string>())
            {
                if (!AIFinalSourceProtocol.TryParseExact(value, out _) ||
                    value == "memory_deterministic" ||
                    value == "server_rule" ||
                    value == "server_knowledge")
                {
                    continue;
                }

                if (result.Count == 0 || result[result.Count - 1] != value)
                {
                    result.Add(value);
                }
            }

            return result.ToArray();
        }

        private static string CleanMemoryStatus(string value)
        {
            switch (value)
            {
                case "not_read":
                case "unavailable":
                case "empty":
                case "available":
                    return value;
                default:
                    return "not_read";
            }
        }

        private static string CleanCode(string value, string fallback)
        {
            if (string.IsNullOrEmpty(value) || value.Length > 64)
            {
                return fallback;
            }

            foreach (var character in value)
            {
                if (!(character == '_' || (character >= 'a' && character <= 'z') ||
                      (character >= '0' && character <= '9')))
                {
                    return fallback;
                }
            }

            return value;
        }
    }
}
#endif
