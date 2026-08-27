using System;
using UnityEngine;

namespace EndangeredAR.AI.OnDevice
{
    [Serializable]
    public sealed class OnDeviceChatMessage
    {
        [SerializeField] private string role;
        [SerializeField] private string content;

        public OnDeviceChatMessage(string role, string content)
        {
            if (role != "system" && role != "user" && role != "assistant")
            {
                throw new ArgumentException("Unsupported chat role.", nameof(role));
            }

            if (string.IsNullOrWhiteSpace(content))
            {
                throw new ArgumentException("Chat content is required.", nameof(content));
            }

            this.role = role;
            this.content = content;
        }

        public string Role => role ?? string.Empty;
        public string Content => content ?? string.Empty;

        internal OnDeviceChatMessage Copy()
        {
            return new OnDeviceChatMessage(Role, Content);
        }
    }
}
