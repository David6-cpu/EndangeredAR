using System;
using UnityEngine;

namespace EndangeredAR.Animals
{
    [CreateAssetMenu(menuName = "Endangered AR/Animal Knowledge Profile")]
    public sealed class AnimalKnowledgeProfile : ScriptableObject
    {
        [SerializeField] private string endangeredLevel;
        [SerializeField] private string habitat;
        [SerializeField] private string food;
        [SerializeField] private string[] threats = Array.Empty<string>();
        [SerializeField] private string[] protectionActions = Array.Empty<string>();
        [SerializeField] private string[] dailyFacts = Array.Empty<string>();
        [SerializeField] private AnimalKnowledgeEntry[] entries = Array.Empty<AnimalKnowledgeEntry>();
        [SerializeField] private string unknownReply;
        [SerializeField] private string[] defaultSuggestions = Array.Empty<string>();

        public string EndangeredLevel => endangeredLevel;
        public string Habitat => habitat;
        public string Food => food;
        public string[] Threats => Copy(threats);
        public string[] ProtectionActions => Copy(protectionActions);
        public string[] DailyFacts => Copy(dailyFacts);
        public AnimalKnowledgeEntry[] Entries => Copy(entries);
        public string UnknownReply => unknownReply;
        public string[] DefaultSuggestions => Copy(defaultSuggestions);

        public bool TryFindAnswer(string message, out AnimalKnowledgeEntry entry)
        {
            if (!string.IsNullOrWhiteSpace(message) && entries != null)
            {
                foreach (var candidate in entries)
                {
                    if (candidate == null || candidate.Keywords == null)
                    {
                        continue;
                    }

                    foreach (var keyword in candidate.Keywords)
                    {
                        if (!string.IsNullOrWhiteSpace(keyword) &&
                            message.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            entry = candidate;
                            return true;
                        }
                    }
                }
            }

            entry = new AnimalKnowledgeEntry(string.Empty, Array.Empty<string>(), unknownReply, defaultSuggestions);
            return false;
        }

        internal void Configure(
            string configuredEndangeredLevel,
            string configuredHabitat,
            string configuredFood,
            string[] configuredThreats,
            string[] configuredProtectionActions,
            string[] configuredDailyFacts,
            AnimalKnowledgeEntry[] configuredEntries,
            string configuredUnknownReply,
            string[] configuredDefaultSuggestions)
        {
            endangeredLevel = configuredEndangeredLevel;
            habitat = configuredHabitat;
            food = configuredFood;
            threats = Copy(configuredThreats);
            protectionActions = Copy(configuredProtectionActions);
            dailyFacts = Copy(configuredDailyFacts);
            entries = Copy(configuredEntries);
            unknownReply = configuredUnknownReply;
            defaultSuggestions = Copy(configuredDefaultSuggestions);
        }

        private static T[] Copy<T>(T[] values)
        {
            return values == null ? Array.Empty<T>() : (T[])values.Clone();
        }
    }

    [Serializable]
    public sealed class AnimalKnowledgeEntry
    {
        [SerializeField] private string knowledgeId;
        [SerializeField] private string[] keywords = Array.Empty<string>();
        [SerializeField] private string reply;
        [SerializeField] private string[] suggestedQuestions = Array.Empty<string>();

        public AnimalKnowledgeEntry(string knowledgeId, string[] keywords, string reply, string[] suggestedQuestions)
        {
            this.knowledgeId = knowledgeId;
            this.keywords = Copy(keywords);
            this.reply = reply;
            this.suggestedQuestions = Copy(suggestedQuestions);
        }

        public string KnowledgeId => knowledgeId;
        public string[] Keywords => Copy(keywords);
        public string Reply => reply;
        public string[] SuggestedQuestions => Copy(suggestedQuestions);

        private static T[] Copy<T>(T[] values)
        {
            return values == null ? Array.Empty<T>() : (T[])values.Clone();
        }
    }
}
