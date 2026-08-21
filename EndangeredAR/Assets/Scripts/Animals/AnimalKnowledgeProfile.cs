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
        [SerializeField] private AnimalKnowledgeSource[] sources = Array.Empty<AnimalKnowledgeSource>();
        [SerializeField] private string unknownReply;
        [SerializeField] private string[] defaultSuggestions = Array.Empty<string>();

        public string EndangeredLevel => endangeredLevel;
        public string Habitat => habitat;
        public string Food => food;
        public string[] Threats => Copy(threats);
        public string[] ProtectionActions => Copy(protectionActions);
        public string[] DailyFacts => Copy(dailyFacts);
        public AnimalKnowledgeEntry[] Entries => Copy(entries);
        public AnimalKnowledgeSource[] Sources => Copy(sources);
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
            Configure(
                configuredEndangeredLevel,
                configuredHabitat,
                configuredFood,
                configuredThreats,
                configuredProtectionActions,
                configuredDailyFacts,
                configuredEntries,
                Array.Empty<AnimalKnowledgeSource>(),
                configuredUnknownReply,
                configuredDefaultSuggestions);
        }

        internal void Configure(
            string configuredEndangeredLevel,
            string configuredHabitat,
            string configuredFood,
            string[] configuredThreats,
            string[] configuredProtectionActions,
            string[] configuredDailyFacts,
            AnimalKnowledgeEntry[] configuredEntries,
            AnimalKnowledgeSource[] configuredSources,
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
            sources = Copy(configuredSources);
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
        [SerializeField] private string topic;
        [SerializeField] private string claim;
        [SerializeField] private string[] keywords = Array.Empty<string>();
        [SerializeField] private string[] aliases = Array.Empty<string>();
        [SerializeField] private string reply;
        [SerializeField] private string displayValue;
        [SerializeField] private string[] items = Array.Empty<string>();
        [SerializeField] private string[] sourceIds = Array.Empty<string>();
        [SerializeField] private string confidence;
        [SerializeField] private string evidenceStatus;
        [SerializeField] private string lastVerified;
        [SerializeField] private string notes;
        [SerializeField] private string[] suggestedQuestions = Array.Empty<string>();

        public AnimalKnowledgeEntry(string knowledgeId, string[] keywords, string reply, string[] suggestedQuestions)
        {
            this.knowledgeId = knowledgeId;
            topic = knowledgeId;
            this.keywords = Copy(keywords);
            this.reply = reply;
            this.suggestedQuestions = Copy(suggestedQuestions);
        }

        public AnimalKnowledgeEntry(
            string knowledgeId,
            string topic,
            string claim,
            string[] keywords,
            string[] aliases,
            string reply,
            string displayValue,
            string[] items,
            string[] sourceIds,
            string confidence,
            string evidenceStatus,
            string lastVerified,
            string notes,
            string[] suggestedQuestions)
        {
            this.knowledgeId = knowledgeId;
            this.topic = topic;
            this.claim = claim;
            this.keywords = Copy(keywords);
            this.aliases = Copy(aliases);
            this.reply = reply;
            this.displayValue = displayValue;
            this.items = Copy(items);
            this.sourceIds = Copy(sourceIds);
            this.confidence = confidence;
            this.evidenceStatus = evidenceStatus;
            this.lastVerified = lastVerified;
            this.notes = notes;
            this.suggestedQuestions = Copy(suggestedQuestions);
        }

        public string KnowledgeId => knowledgeId;
        public string Topic => topic;
        public string Claim => claim;
        public string[] Keywords => Copy(keywords);
        public string[] Aliases => Copy(aliases);
        public string Reply => reply;
        public string DisplayValue => displayValue;
        public string[] Items => Copy(items);
        public string[] SourceIds => Copy(sourceIds);
        public string Confidence => confidence;
        public string EvidenceStatus => evidenceStatus;
        public string LastVerified => lastVerified;
        public string Notes => notes;
        public string[] SuggestedQuestions => Copy(suggestedQuestions);

        private static T[] Copy<T>(T[] values)
        {
            return values == null ? Array.Empty<T>() : (T[])values.Clone();
        }
    }

    [Serializable]
    public sealed class AnimalKnowledgeSource
    {
        [SerializeField] private string sourceId;
        [SerializeField] private string title;
        [SerializeField] private string organization;
        [SerializeField] private string sourceType;
        [SerializeField] private string url;
        [SerializeField] private string publishedOrUpdatedDate;
        [SerializeField] private string projectVerifiedDate;
        [SerializeField] private string[] appliesToFactIds = Array.Empty<string>();
        [SerializeField] private string notes;

        public AnimalKnowledgeSource(
            string sourceId,
            string title,
            string organization,
            string sourceType,
            string url,
            string publishedOrUpdatedDate,
            string projectVerifiedDate,
            string[] appliesToFactIds,
            string notes)
        {
            this.sourceId = sourceId;
            this.title = title;
            this.organization = organization;
            this.sourceType = sourceType;
            this.url = url;
            this.publishedOrUpdatedDate = publishedOrUpdatedDate;
            this.projectVerifiedDate = projectVerifiedDate;
            this.appliesToFactIds = Copy(appliesToFactIds);
            this.notes = notes;
        }

        public string SourceId => sourceId;
        public string Title => title;
        public string Organization => organization;
        public string SourceType => sourceType;
        public string Url => url;
        public string PublishedOrUpdatedDate => publishedOrUpdatedDate;
        public string ProjectVerifiedDate => projectVerifiedDate;
        public string[] AppliesToFactIds => Copy(appliesToFactIds);
        public string Notes => notes;

        private static T[] Copy<T>(T[] values)
        {
            return values == null ? Array.Empty<T>() : (T[])values.Clone();
        }
    }
}
