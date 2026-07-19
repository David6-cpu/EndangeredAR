using System;
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
            if (defaultProfile != null)
            {
                return defaultProfile;
            }

            foreach (var definition in Resources.LoadAll<AnimalDefinition>("Animals"))
            {
                if (definition != null && definition.Knowledge != null)
                {
                    return definition.Knowledge;
                }
            }

            foreach (var profile in Resources.LoadAll<AnimalKnowledgeProfile>("Animals"))
            {
                if (profile != null)
                {
                    return profile;
                }
            }

            return null;
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
