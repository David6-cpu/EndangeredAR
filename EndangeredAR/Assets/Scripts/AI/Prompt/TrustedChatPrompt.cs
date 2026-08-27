using System;
using System.Collections.Generic;
using EndangeredAR.AI.OnDevice;

namespace EndangeredAR.AI.Prompt
{
    public sealed class TrustedChatPromptInput
    {
        private readonly OnDeviceChatMessage[] sessionHistory;

        public TrustedChatPromptInput(
            string systemRole,
            string currentUserMessage,
            IReadOnlyList<OnDeviceChatMessage> sessionHistory,
            ContentAuthority contentAuthority,
            string currentReadOnlyState,
            string pastCharacterMemory,
            string canonicalEvidence,
            string systemPolicy)
        {
            if (string.IsNullOrWhiteSpace(systemRole) || string.IsNullOrWhiteSpace(currentUserMessage))
            {
                throw new ArgumentException("System role and current user message are required.");
            }

            SystemRole = systemRole;
            CurrentUserMessage = currentUserMessage;
            this.sessionHistory = CopyHistory(sessionHistory);
            ContentAuthority = contentAuthority;
            CurrentReadOnlyState = currentReadOnlyState ?? string.Empty;
            PastCharacterMemory = pastCharacterMemory ?? string.Empty;
            CanonicalEvidence = canonicalEvidence ?? string.Empty;
            SystemPolicy = systemPolicy ?? string.Empty;
        }

        public string SystemRole { get; }
        public string CurrentUserMessage { get; }
        public IReadOnlyList<OnDeviceChatMessage> SessionHistory => Array.AsReadOnly(Copy(sessionHistory));
        public ContentAuthority ContentAuthority { get; }
        public string CurrentReadOnlyState { get; }
        public string PastCharacterMemory { get; }
        public string CanonicalEvidence { get; }
        public string SystemPolicy { get; }

        private static OnDeviceChatMessage[] CopyHistory(IReadOnlyList<OnDeviceChatMessage> values)
        {
            if (values == null || values.Count == 0)
            {
                return Array.Empty<OnDeviceChatMessage>();
            }

            var copy = new OnDeviceChatMessage[values.Count];
            for (var index = 0; index < values.Count; index++)
            {
                var value = values[index] ?? throw new ArgumentException("History cannot contain null messages.");
                if (value.Role != "user" && value.Role != "assistant")
                {
                    throw new ArgumentException("Session history can contain only user and assistant roles.");
                }

                copy[index] = value.Copy();
            }

            return copy;
        }

        private static OnDeviceChatMessage[] Copy(IReadOnlyList<OnDeviceChatMessage> values)
        {
            if (values == null || values.Count == 0)
            {
                return Array.Empty<OnDeviceChatMessage>();
            }

            var copy = new OnDeviceChatMessage[values.Count];
            for (var index = 0; index < values.Count; index++)
            {
                copy[index] = values[index].Copy();
            }

            return copy;
        }
    }

    public sealed class TrustedChatPrompt
    {
        private readonly OnDeviceChatMessage[] messages;
        private readonly TrustedPromptSection[] includedSections;

        internal TrustedChatPrompt(
            IReadOnlyList<OnDeviceChatMessage> messages,
            IReadOnlyList<TrustedPromptSection> includedSections,
            int promptTokens,
            int droppedHistoryMessages)
        {
            this.messages = Copy(messages);
            this.includedSections = Copy(includedSections);
            PromptTokens = promptTokens;
            DroppedHistoryMessages = droppedHistoryMessages;
        }

        public IReadOnlyList<OnDeviceChatMessage> Messages => Array.AsReadOnly(Copy(messages));
        public IReadOnlyList<TrustedPromptSection> IncludedSections => Array.AsReadOnly(Copy(includedSections));
        public int PromptTokens { get; }
        public int DroppedHistoryMessages { get; }

        private static OnDeviceChatMessage[] Copy(IReadOnlyList<OnDeviceChatMessage> values)
        {
            var copy = new OnDeviceChatMessage[values?.Count ?? 0];
            for (var index = 0; index < copy.Length; index++)
            {
                copy[index] = values[index].Copy();
            }

            return copy;
        }

        private static TrustedPromptSection[] Copy(IReadOnlyList<TrustedPromptSection> values)
        {
            var copy = new TrustedPromptSection[values?.Count ?? 0];
            for (var index = 0; index < copy.Length; index++)
            {
                copy[index] = values[index];
            }

            return copy;
        }
    }
}
