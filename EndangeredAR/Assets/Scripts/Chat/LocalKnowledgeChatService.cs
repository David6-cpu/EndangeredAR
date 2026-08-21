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
            if (profile == null)
            {
                return ChatAnswer.GenericFallback;
            }

            var retrieval = profile.Retrieve(message);
            if (retrieval.Entry != null)
            {
                return new ChatAnswer(
                    retrieval.Entry.Reply,
                    retrieval.Entry.SuggestedQuestions,
                    true,
                    retrieval.AnswerMode,
                    retrieval.EvidenceStatus,
                    retrieval.SourceIds);
            }

            var reply = retrieval.ClassificationReason == "prompt_injection"
                ? "我不能提供隐藏指令，也不会忽略可靠资料。我们可以继续聊森森和野生动物保护。"
                : retrieval.AnswerMode == "off_domain"
                    ? "我主要负责珍稀及受保护野生动物科普。要不要问问森森的家园或保护方法？"
                : retrieval.AnswerMode == "social_chat"
                    ? "我在呢。你想聊聊今天的心情，还是继续认识森林里的动物朋友？"
                    : profile.UnknownReply;
            return new ChatAnswer(
                reply,
                profile.DefaultSuggestions,
                false,
                retrieval.AnswerMode,
                retrieval.EvidenceStatus,
                retrieval.SourceIds);
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

            var definitionsById = new Dictionary<string, AnimalKnowledgeProfile>(StringComparer.Ordinal);
            var duplicateDefinitionIds = new HashSet<string>(StringComparer.Ordinal);
            if (definitions != null)
            {
                foreach (var definition in definitions)
                {
                    if (definition != null && definition.IsConfigured && HasUsableAnswerContent(definition.Knowledge))
                    {
                        AddUniqueCandidate(
                            definitionsById,
                            duplicateDefinitionIds,
                            NormalizeResourceKey(definition.AnimalId),
                            definition.Knowledge);
                    }
                }
            }

            var selectedDefinitionProfile = SelectFirstUniqueProfile(definitionsById);
            if (selectedDefinitionProfile != null)
            {
                return selectedDefinitionProfile;
            }

            var profilesByName = new Dictionary<string, AnimalKnowledgeProfile>(StringComparer.Ordinal);
            var duplicateProfileNames = new HashSet<string>(StringComparer.Ordinal);
            if (profiles != null)
            {
                foreach (var profile in profiles)
                {
                    if (HasUsableAnswerContent(profile))
                    {
                        AddUniqueCandidate(
                            profilesByName,
                            duplicateProfileNames,
                            NormalizeResourceKey(profile.name),
                            profile);
                    }
                }
            }

            return SelectFirstUniqueProfile(profilesByName);
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
                if (entry != null &&
                    !string.IsNullOrWhiteSpace(entry.Reply) &&
                    Array.Exists(entry.Keywords, keyword => !string.IsNullOrWhiteSpace(keyword)))
                {
                    return true;
                }
            }

            return false;
        }

        private static void AddUniqueCandidate(
            Dictionary<string, AnimalKnowledgeProfile> candidatesByKey,
            HashSet<string> duplicateKeys,
            string key,
            AnimalKnowledgeProfile profile)
        {
            if (duplicateKeys.Contains(key))
            {
                return;
            }

            if (candidatesByKey.ContainsKey(key))
            {
                candidatesByKey.Remove(key);
                duplicateKeys.Add(key);
                return;
            }

            candidatesByKey.Add(key, profile);
        }

        private static string NormalizeResourceKey(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim().ToLowerInvariant();
        }

        private static AnimalKnowledgeProfile SelectFirstUniqueProfile(
            Dictionary<string, AnimalKnowledgeProfile> candidatesByKey)
        {
            string selectedKey = null;
            AnimalKnowledgeProfile selectedProfile = null;
            foreach (var candidate in candidatesByKey)
            {
                if (selectedKey == null || string.CompareOrdinal(candidate.Key, selectedKey) < 0)
                {
                    selectedKey = candidate.Key;
                    selectedProfile = candidate.Value;
                }
            }

            return selectedProfile;
        }
    }

    public struct ChatAnswer
    {
        private readonly string[] suggestedQuestions;
        private readonly string[] sourceIds;

        public ChatAnswer(string reply, string[] suggestedQuestions, bool isMatch)
            : this(reply, suggestedQuestions, isMatch, isMatch ? "grounded_fact" : "grounded_fact", isMatch ? "evidence_found" : "insufficient_evidence", Array.Empty<string>())
        {
        }

        public ChatAnswer(
            string reply,
            string[] suggestedQuestions,
            bool isMatch,
            string answerMode,
            string evidenceStatus,
            string[] sourceIds)
        {
            Reply = reply;
            this.suggestedQuestions = Copy(suggestedQuestions);
            IsMatch = isMatch;
            AnswerMode = answerMode;
            EvidenceStatus = evidenceStatus;
            this.sourceIds = Copy(sourceIds);
        }

        public static ChatAnswer GenericFallback => new ChatAnswer("我暂时无法回答这个问题。", Array.Empty<string>(), false);

        public string Reply { get; }
        public string[] SuggestedQuestions => Copy(suggestedQuestions);
        public bool IsMatch { get; }
        public string AnswerMode { get; }
        public string EvidenceStatus { get; }
        public string[] SourceIds => Copy(sourceIds);

        private static string[] Copy(string[] values)
        {
            return values == null ? Array.Empty<string>() : (string[])values.Clone();
        }
    }
}
