using System;
using System.Collections.Generic;
using System.Text;
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
            var retrieval = Retrieve(message);
            if (retrieval.Entry != null)
            {
                entry = retrieval.Entry;
                return true;
            }

            entry = new AnimalKnowledgeEntry(string.Empty, Array.Empty<string>(), unknownReply, defaultSuggestions);
            return false;
        }

        public AnimalKnowledgeRetrieval Retrieve(string message)
        {
            var normalized = Normalize(message);
            if (string.IsNullOrEmpty(normalized))
            {
                return AnimalKnowledgeRetrieval.Insufficient("empty_question");
            }

            if (ContainsAny(normalized, SocialMarkers))
            {
                return AnimalKnowledgeRetrieval.Social;
            }

            AnimalKnowledgeEntry selected = null;
            var selectedScore = 0;
            foreach (var candidate in entries ?? Array.Empty<AnimalKnowledgeEntry>())
            {
                var score = Score(candidate, normalized);
                if (score > selectedScore)
                {
                    selectedScore = score;
                    selected = candidate;
                }
            }

            if (selected != null)
            {
                var status = selected.EvidenceStatus == "known_unknown"
                    ? "insufficient_evidence"
                    : "evidence_found";
                return new AnimalKnowledgeRetrieval(
                    selected,
                    "grounded_fact",
                    status,
                    selected.SourceIds,
                    $"matched_{selected.Topic}");
            }

            if (ContainsAny(normalized, OffDomainMarkers))
            {
                return AnimalKnowledgeRetrieval.OffDomain;
            }

            if (ContainsAny(normalized, InjectionMarkers) || ContainsAny(normalized, ScientificMarkers))
            {
                return AnimalKnowledgeRetrieval.Insufficient("unmatched_scientific_question");
            }

            return sources != null && sources.Length > 0
                ? AnimalKnowledgeRetrieval.Social
                : AnimalKnowledgeRetrieval.Insufficient("legacy_profile_fallback");
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

        private static int Score(AnimalKnowledgeEntry entry, string normalizedMessage)
        {
            if (entry == null)
            {
                return 0;
            }

            var score = ScoreTerms(entry.Aliases, normalizedMessage, 100);
            return Math.Max(score, ScoreTerms(entry.Keywords, normalizedMessage, 0));
        }

        private static int ScoreTerms(string[] terms, string normalizedMessage, int categoryBonus)
        {
            var best = 0;
            foreach (var term in terms ?? Array.Empty<string>())
            {
                var normalizedTerm = Normalize(term);
                if (string.IsNullOrEmpty(normalizedTerm) || normalizedMessage.IndexOf(normalizedTerm, StringComparison.Ordinal) < 0)
                {
                    continue;
                }

                var exactBonus = normalizedMessage == normalizedTerm ? 1000 : 0;
                best = Math.Max(best, exactBonus + categoryBonus + normalizedTerm.Length);
            }

            return best;
        }

        private static bool ContainsAny(string normalizedMessage, IEnumerable<string> markers)
        {
            foreach (var marker in markers)
            {
                if (normalizedMessage.IndexOf(Normalize(marker), StringComparison.Ordinal) >= 0)
                {
                    return true;
                }
            }

            return false;
        }

        private static string Normalize(string value)
        {
            var builder = new StringBuilder();
            foreach (var character in (value ?? string.Empty).ToLowerInvariant())
            {
                if (char.IsLetterOrDigit(character))
                {
                    builder.Append(character);
                }
            }

            return builder.ToString();
        }

        private static readonly string[] SocialMarkers =
        {
            "你好", "谢谢", "再见", "难过", "伤心", "开心", "陪我", "聊聊", "喜欢我", "语气", "讲个故事", "介绍自己"
        };

        private static readonly string[] OffDomainMarkers =
        {
            "二次方程", "数学题", "写代码", "编程", "股票", "投资", "天气", "翻译", "写作文", "做作业"
        };

        private static readonly string[] InjectionMarkers =
        {
            "忽略系统", "忽略规则", "忽略资料", "忽略以上", "绕过规则", "不要根据资料", "假装你确定", "知识库", "编造", "编一个"
        };

        private static readonly string[] ScientificMarkers =
        {
            "学名", "分类", "分布", "栖息", "住", "生活", "吃", "食物", "食性", "行为", "习性", "威胁", "危险", "变少", "数量", "多少", "几只", "保护", "等级", "近危", "濒危", "会", "能不能", "是否", "为什么"
        };
    }

    public sealed class AnimalKnowledgeRetrieval
    {
        private readonly string[] sourceIds;

        public AnimalKnowledgeRetrieval(
            AnimalKnowledgeEntry entry,
            string answerMode,
            string evidenceStatus,
            string[] sourceIds,
            string classificationReason)
        {
            Entry = entry;
            AnswerMode = answerMode;
            EvidenceStatus = evidenceStatus;
            this.sourceIds = sourceIds == null ? Array.Empty<string>() : (string[])sourceIds.Clone();
            ClassificationReason = classificationReason;
        }

        public static AnimalKnowledgeRetrieval Social => new AnimalKnowledgeRetrieval(
            null, "social_chat", "not_required", Array.Empty<string>(), "social_chat");

        public static AnimalKnowledgeRetrieval OffDomain => new AnimalKnowledgeRetrieval(
            null, "off_domain", "not_required", Array.Empty<string>(), "off_domain_marker");

        public static AnimalKnowledgeRetrieval Insufficient(string reason) => new AnimalKnowledgeRetrieval(
            null, "grounded_fact", "insufficient_evidence", Array.Empty<string>(), reason);

        public AnimalKnowledgeEntry Entry { get; }
        public string AnswerMode { get; }
        public string EvidenceStatus { get; }
        public string[] SourceIds => (string[])sourceIds.Clone();
        public string ClassificationReason { get; }
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
