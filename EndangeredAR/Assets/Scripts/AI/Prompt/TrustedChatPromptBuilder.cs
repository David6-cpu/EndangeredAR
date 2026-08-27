using System;
using System.Collections.Generic;
using System.Text;
using EndangeredAR.AI.OnDevice;

namespace EndangeredAR.AI.Prompt
{
    public static class TrustedChatPromptBuilder
    {
        public static TrustedChatPrompt Build(
            TrustedChatPromptInput input,
            OnDevicePromptBudget budget,
            IOnDeviceTokenCounter tokenCounter)
        {
            if (input == null || budget == null || tokenCounter == null)
            {
                throw new ArgumentNullException();
            }

            var includedSections = new List<TrustedPromptSection>
            {
                TrustedPromptSection.SystemRole,
                TrustedPromptSection.CurrentUserMessage
            };
            var system = new StringBuilder(input.SystemRole.Trim());
            AppendAuthoritySection(system, includedSections, input);

            var history = new List<OnDeviceChatMessage>();
            foreach (var message in input.SessionHistory)
            {
                history.Add(message.Copy());
            }

            var dropped = 0;
            while (true)
            {
                var candidate = Compose(system.ToString(), history, input.CurrentUserMessage);
                var count = tokenCounter.CountTokens(candidate);
                if (count < 0)
                {
                    throw new OnDevicePromptBudgetExceededException();
                }

                if (count <= budget.MaximumPromptTokens)
                {
                    if (history.Count > 0)
                    {
                        includedSections.Add(TrustedPromptSection.SessionHistory);
                    }

                    return new TrustedChatPrompt(candidate, includedSections, count, dropped);
                }

                if (history.Count == 0)
                {
                    throw new OnDevicePromptBudgetExceededException();
                }

                var removedRole = history[0].Role;
                history.RemoveAt(0);
                dropped++;
                if (removedRole == "user" && history.Count > 0 && history[0].Role == "assistant")
                {
                    history.RemoveAt(0);
                    dropped++;
                }
                else
                {
                    while (history.Count > 0 && history[0].Role == "assistant")
                    {
                        history.RemoveAt(0);
                        dropped++;
                    }
                }
            }
        }

        private static void AppendAuthoritySection(
            StringBuilder system,
            List<TrustedPromptSection> included,
            TrustedChatPromptInput input)
        {
            switch (input.ContentAuthority)
            {
                case ContentAuthority.CurrentProgress:
                    Append(system, "CURRENT READ-ONLY STATE", input.CurrentReadOnlyState);
                    included.Add(TrustedPromptSection.CurrentReadOnlyState);
                    break;
                case ContentAuthority.CharacterMemory:
                    Append(system, "PAST CHARACTER MEMORY", input.PastCharacterMemory);
                    included.Add(TrustedPromptSection.PastCharacterMemory);
                    break;
                case ContentAuthority.CanonicalKnowledge:
                    Append(system, "CANONICAL EVIDENCE", input.CanonicalEvidence);
                    included.Add(TrustedPromptSection.CanonicalEvidence);
                    break;
                case ContentAuthority.SystemPolicy:
                    Append(system, "SYSTEM POLICY", input.SystemPolicy);
                    included.Add(TrustedPromptSection.SystemPolicy);
                    break;
            }
        }

        private static void Append(StringBuilder builder, string label, string content)
        {
            if (string.IsNullOrWhiteSpace(content))
            {
                throw new ArgumentException("The selected authority section is empty.");
            }

            builder.Append("\n\n<").Append(label).Append(">\n")
                .Append(content.Trim())
                .Append("\n</").Append(label).Append('>');
        }

        private static OnDeviceChatMessage[] Compose(
            string system,
            IReadOnlyList<OnDeviceChatMessage> history,
            string currentUserMessage)
        {
            var messages = new OnDeviceChatMessage[(history?.Count ?? 0) + 2];
            messages[0] = new OnDeviceChatMessage("system", system);
            for (var index = 0; index < (history?.Count ?? 0); index++)
            {
                messages[index + 1] = history[index].Copy();
            }

            messages[messages.Length - 1] = new OnDeviceChatMessage("user", currentUserMessage.Trim());
            return messages;
        }
    }
}
