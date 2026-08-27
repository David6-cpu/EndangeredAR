using System;
using System.Collections.Generic;
using UnityEngine;

namespace EndangeredAR.AI.OnDevice
{
    public sealed class OnDeviceLLMRequest
    {
        private readonly OnDeviceChatMessage[] messages;

        public OnDeviceLLMRequest(
            string generationId,
            IReadOnlyList<OnDeviceChatMessage> messages,
            int maxTokens,
            float temperature,
            float topP,
            float repeatPenalty,
            uint seed)
        {
            if (!IsSafeGenerationId(generationId))
            {
                throw new ArgumentException("Generation identity is invalid.", nameof(generationId));
            }

            if (messages == null || messages.Count == 0 || messages.Count > 64)
            {
                throw new ArgumentException("A bounded message list is required.", nameof(messages));
            }

            if (maxTokens < 1 || maxTokens > 256)
            {
                throw new ArgumentOutOfRangeException(nameof(maxTokens));
            }

            if (float.IsNaN(temperature) || float.IsInfinity(temperature) || temperature < 0f || temperature > 2f ||
                float.IsNaN(topP) || float.IsInfinity(topP) || topP <= 0f || topP > 1f ||
                float.IsNaN(repeatPenalty) || float.IsInfinity(repeatPenalty) || repeatPenalty < 0.5f || repeatPenalty > 2f)
            {
                throw new ArgumentOutOfRangeException(nameof(temperature));
            }

            GenerationId = generationId;
            this.messages = Copy(messages);
            MaxTokens = maxTokens;
            Temperature = temperature;
            TopP = topP;
            RepeatPenalty = repeatPenalty;
            Seed = seed;
        }

        public string GenerationId { get; }
        public IReadOnlyList<OnDeviceChatMessage> Messages => Array.AsReadOnly(Copy(messages));
        public int MaxTokens { get; }
        public float Temperature { get; }
        public float TopP { get; }
        public float RepeatPenalty { get; }
        public uint Seed { get; }

        internal string SerializeMessages()
        {
            return JsonUtility.ToJson(new MessageEnvelope { messages = Copy(messages) });
        }

        private static bool IsSafeGenerationId(string value)
        {
            if (string.IsNullOrEmpty(value) || value.Length > 64)
            {
                return false;
            }

            foreach (var character in value)
            {
                if (!(character == '_' || character == '-' ||
                      character >= 'a' && character <= 'z' ||
                      character >= 'A' && character <= 'Z' ||
                      character >= '0' && character <= '9'))
                {
                    return false;
                }
            }

            return true;
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
                copy[index] = values[index]?.Copy() ??
                              throw new ArgumentException("Chat messages cannot contain null values.", nameof(values));
            }

            return copy;
        }

        [Serializable]
        private sealed class MessageEnvelope
        {
            public OnDeviceChatMessage[] messages = Array.Empty<OnDeviceChatMessage>();
        }
    }
}
