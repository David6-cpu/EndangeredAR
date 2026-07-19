using System;
using System.Collections.Generic;
using EndangeredAR.Animals;
using UnityEngine;

namespace EndangeredAR.Chat
{
    public class LocalKnowledgeChatService : MonoBehaviour
    {
        [SerializeField] private AnimalKnowledgeProfile defaultProfile;

        public ChatAnswer Answer(AnimalKnowledgeProfile profile, string message)
        {
            if (profile != null && profile.TryFindAnswer(message, out var entry))
            {
                return new ChatAnswer(entry.Reply, entry.SuggestedQuestions, true);
            }

            return profile == null
                ? ChatAnswer.GenericFallback
                : new ChatAnswer(profile.UnknownReply, profile.DefaultSuggestions, false);
        }

        [Obsolete("Use Answer(AnimalKnowledgeProfile, string) with the active animal profile.")]
        public ChatAnswer Answer(string message)
        {
            return Answer(ResolveDefaultProfile(), message);
        }

        private AnimalKnowledgeProfile ResolveDefaultProfile()
        {
            return SelectLegacyProfile(
                defaultProfile,
                Resources.LoadAll<AnimalDefinition>("Animals"),
                Resources.LoadAll<AnimalKnowledgeProfile>("Animals"));
        }

        internal static AnimalKnowledgeProfile SelectLegacyProfile(
            AnimalKnowledgeProfile serializedDefaultProfile,
            IEnumerable<AnimalDefinition> definitions,
            IEnumerable<AnimalKnowledgeProfile> profiles)
        {
            if (HasUsableAnswerContent(serializedDefaultProfile))
            {
                return serializedDefaultProfile;
            }

            var validDefinitions = new List<AnimalDefinition>();
            if (definitions != null)
            {
                foreach (var definition in definitions)
                {
                    if (definition != null && definition.IsConfigured && HasUsableAnswerContent(definition.Knowledge))
                    {
                        validDefinitions.Add(definition);
                    }
                }
            }

            validDefinitions.Sort(CompareDefinitionsByAnimalId);
            if (validDefinitions.Count > 0)
            {
                return validDefinitions[0].Knowledge;
            }

            var validProfiles = new List<AnimalKnowledgeProfile>();
            if (profiles != null)
            {
                foreach (var profile in profiles)
                {
                    if (HasUsableAnswerContent(profile))
                    {
                        validProfiles.Add(profile);
                    }
                }
            }

            validProfiles.Sort(CompareProfilesByName);
            if (validProfiles.Count > 0)
            {
                return validProfiles[0];
            }

            return null;
        }

        private static bool HasUsableAnswerContent(AnimalKnowledgeProfile profile)
        {
            if (profile == null)
            {
                return false;
            }

            if (!string.IsNullOrWhiteSpace(profile.UnknownReply))
            {
                return true;
            }

            foreach (var entry in profile.Entries)
            {
                if (entry != null && !string.IsNullOrWhiteSpace(entry.Reply))
                {
                    return true;
                }
            }

            return false;
        }

        private static int CompareDefinitionsByAnimalId(AnimalDefinition first, AnimalDefinition second)
        {
            var comparison = string.Compare(first.AnimalId, second.AnimalId, StringComparison.OrdinalIgnoreCase);
            return comparison != 0
                ? comparison
                : string.Compare(first.AnimalId, second.AnimalId, StringComparison.Ordinal);
        }

        private static int CompareProfilesByName(AnimalKnowledgeProfile first, AnimalKnowledgeProfile second)
        {
            var comparison = string.Compare(first.name, second.name, StringComparison.OrdinalIgnoreCase);
            return comparison != 0
                ? comparison
                : string.Compare(first.name, second.name, StringComparison.Ordinal);
        }
    }

    public struct ChatAnswer
    {
        private readonly string[] suggestedQuestions;

        public ChatAnswer(string reply, string[] suggestedQuestions, bool isMatch)
        {
            Reply = reply;
            this.suggestedQuestions = Copy(suggestedQuestions);
            IsMatch = isMatch;
        }

        public static ChatAnswer GenericFallback => new ChatAnswer("我暂时无法回答这个问题。", Array.Empty<string>(), false);

        public string Reply { get; }
        public string[] SuggestedQuestions => Copy(suggestedQuestions);
        public bool IsMatch { get; }

        private static string[] Copy(string[] values)
        {
            return values == null ? Array.Empty<string>() : (string[])values.Clone();
        }
    }
}
