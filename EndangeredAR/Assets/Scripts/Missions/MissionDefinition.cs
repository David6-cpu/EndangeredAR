using System;
using System.Collections.Generic;
using UnityEngine;

namespace EndangeredAR.Missions
{
    [CreateAssetMenu(menuName = "Endangered AR/Mission Definition")]
    public sealed class MissionDefinition : ScriptableObject
    {
        [SerializeField] private string missionId;
        [SerializeField] private string title;
        [SerializeField] private string prompt;
        [SerializeField] private MissionOptionDefinition[] options = Array.Empty<MissionOptionDefinition>();
        [SerializeField] private string correctFeedback;
        [SerializeField] private string wrongFeedback;
        [SerializeField] private string learnedKnowledgeId;
        [SerializeField] private string learnedFact;
        [SerializeField] private string badgeId;
        [SerializeField] private int points;

        public string MissionId => missionId?.Trim();
        public string Title => title;
        public string Prompt => prompt;
        public MissionOptionDefinition[] Options => Copy(options);
        public string CorrectFeedback => correctFeedback;
        public string WrongFeedback => wrongFeedback;
        public string LearnedKnowledgeId => learnedKnowledgeId;
        public string LearnedFact => learnedFact;
        public string BadgeId => badgeId;
        public int Points => points;

        public bool TryGetOption(string optionId, out MissionOptionDefinition option)
        {
            var normalizedOptionId = optionId?.Trim();
            if (!string.IsNullOrWhiteSpace(normalizedOptionId) && options != null)
            {
                foreach (var candidate in options)
                {
                    if (candidate != null &&
                        string.Equals(candidate.OptionId, normalizedOptionId, StringComparison.OrdinalIgnoreCase))
                    {
                        option = candidate;
                        return true;
                    }
                }
            }

            option = null;
            return false;
        }

        public IReadOnlyList<string> Validate()
        {
            var issues = new List<string>();
            if (string.IsNullOrWhiteSpace(MissionId))
            {
                issues.Add("Mission ID is required.");
            }

            var optionIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var hasCorrectOption = false;
            if (options != null)
            {
                foreach (var option in options)
                {
                    if (option == null || string.IsNullOrWhiteSpace(option.OptionId))
                    {
                        issues.Add("Mission option ID is required.");
                        continue;
                    }

                    if (!optionIds.Add(option.OptionId))
                    {
                        issues.Add($"Mission contains duplicate option ID '{option.OptionId}'.");
                    }

                    hasCorrectOption |= option.IsCorrect;
                }
            }

            if (!hasCorrectOption)
            {
                issues.Add("Mission requires at least one correct option.");
            }

            return issues;
        }

        internal void Configure(
            string configuredMissionId,
            string configuredTitle,
            string configuredPrompt,
            MissionOptionDefinition[] configuredOptions,
            string configuredCorrectFeedback,
            string configuredWrongFeedback,
            string configuredLearnedKnowledgeId,
            string configuredLearnedFact,
            string configuredBadgeId,
            int configuredPoints)
        {
            missionId = configuredMissionId;
            title = configuredTitle;
            prompt = configuredPrompt;
            options = Copy(configuredOptions);
            correctFeedback = configuredCorrectFeedback;
            wrongFeedback = configuredWrongFeedback;
            learnedKnowledgeId = configuredLearnedKnowledgeId;
            learnedFact = configuredLearnedFact;
            badgeId = configuredBadgeId;
            points = configuredPoints;
        }

        private static T[] Copy<T>(T[] values)
        {
            return values == null ? Array.Empty<T>() : (T[])values.Clone();
        }
    }

    [Serializable]
    public sealed class MissionOptionDefinition
    {
        [SerializeField] private string optionId;
        [SerializeField] private string label;
        [SerializeField] private bool isCorrect;

        public MissionOptionDefinition(string optionId, string label, bool isCorrect)
        {
            this.optionId = optionId;
            this.label = label;
            this.isCorrect = isCorrect;
        }

        public string OptionId => optionId?.Trim();
        public string Label => label;
        public bool IsCorrect => isCorrect;
    }
}
