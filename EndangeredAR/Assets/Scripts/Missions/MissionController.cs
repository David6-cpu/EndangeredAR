using System;
using UnityEngine;

namespace EndangeredAR.Missions
{
    public class MissionController : MonoBehaviour
    {
        public enum MissionState
        {
            NotStarted,
            Choosing,
            Correct,
            Wrong,
            Completed
        }

        [SerializeField] private MissionDefinition definition;
        [SerializeField] private int points;
        [SerializeField] private MissionState state = MissionState.NotStarted;
        private bool rewardAlreadyClaimed;

        public string CurrentMissionId => definition == null ? string.Empty : definition.MissionId;
        public int Points => points;
        public MissionState State => state;
        public bool IsCompleted => state == MissionState.Completed;

        public void Configure(MissionDefinition configuredDefinition, bool alreadyCompleted = false)
        {
            var missionChanged = !IsSameMission(configuredDefinition);
            definition = configuredDefinition;

            if (missionChanged)
            {
                points = 0;
                rewardAlreadyClaimed = definition != null && alreadyCompleted;
                state = MissionState.NotStarted;
                return;
            }

            if (definition != null && alreadyCompleted)
            {
                rewardAlreadyClaimed = true;
            }
        }

        public void StartMission()
        {
            if (definition == null)
            {
                return;
            }

            state = MissionState.Choosing;
        }

        public void StartFoodMission()
        {
            ConfigureLegacyDefinitionIfNeeded();
            StartMission();
        }

        public MissionResult SelectOption(string optionId)
        {
            if (definition == null || !definition.TryGetOption(optionId, out var option))
            {
                return default;
            }

            if (state == MissionState.Completed)
            {
                return option.IsCorrect
                    ? CreateResult(true, definition.CorrectFeedback, 0)
                    : default;
            }

            if (state != MissionState.Choosing && state != MissionState.Wrong)
            {
                StartMission();
            }

            if (state != MissionState.Choosing && state != MissionState.Wrong)
            {
                return default;
            }

            if (!option.IsCorrect)
            {
                state = MissionState.Wrong;
                return CreateResult(false, definition.WrongFeedback, 0);
            }

            state = MissionState.Completed;
            var pointsAwarded = rewardAlreadyClaimed ? 0 : definition.Points;
            rewardAlreadyClaimed = true;
            points += pointsAwarded;
            return CreateResult(true, definition.CorrectFeedback, pointsAwarded);
        }

        public MissionResult SelectFood(string option)
        {
            ConfigureLegacyDefinitionIfNeeded();

            if (definition != null && !string.IsNullOrWhiteSpace(option))
            {
                var normalizedOption = option.Trim();
                foreach (var candidate in definition.Options)
                {
                    if (candidate != null &&
                        string.Equals(candidate.Label?.Trim(), normalizedOption, StringComparison.OrdinalIgnoreCase))
                    {
                        return SelectOption(candidate.OptionId);
                    }
                }
            }

            return default;
        }

        private void ConfigureLegacyDefinitionIfNeeded()
        {
            if (definition != null)
            {
                return;
            }

            foreach (var candidate in Resources.LoadAll<MissionDefinition>("Animals"))
            {
                if (candidate != null &&
                    !string.IsNullOrWhiteSpace(candidate.MissionId) &&
                    candidate.Validate().Count == 0)
                {
                    Configure(candidate);
                    return;
                }
            }
        }

        public void CompleteCurrentMission()
        {
            if (definition == null || state == MissionState.Completed)
            {
                return;
            }

            foreach (var option in definition.Options)
            {
                if (option != null && option.IsCorrect)
                {
                    SelectOption(option.OptionId);
                    return;
                }
            }
        }

        private MissionResult CreateResult(bool success, string feedback, int pointsAwarded)
        {
            return new MissionResult(
                success,
                feedback,
                definition.LearnedFact,
                definition.LearnedKnowledgeId,
                definition.BadgeId,
                pointsAwarded);
        }

        private bool IsSameMission(MissionDefinition configuredDefinition)
        {
            if (definition == null || configuredDefinition == null)
            {
                return ReferenceEquals(definition, configuredDefinition);
            }

            var currentMissionId = definition.MissionId;
            var configuredMissionId = configuredDefinition.MissionId;
            if (!string.IsNullOrWhiteSpace(currentMissionId) &&
                !string.IsNullOrWhiteSpace(configuredMissionId))
            {
                return string.Equals(currentMissionId, configuredMissionId, StringComparison.OrdinalIgnoreCase);
            }

            return ReferenceEquals(definition, configuredDefinition);
        }
    }

    public struct MissionResult
    {
        public MissionResult(
            bool success,
            string feedback,
            string learnedFact,
            string learnedKnowledgeId,
            string badgeId,
            int pointsAwarded)
        {
            Success = success;
            Feedback = feedback;
            LearnedFact = learnedFact;
            LearnedKnowledgeId = learnedKnowledgeId;
            BadgeId = badgeId;
            PointsAwarded = pointsAwarded;
        }

        public bool Success { get; }
        public string Feedback { get; }
        public string LearnedFact { get; }
        public string LearnedKnowledgeId { get; }
        public string BadgeId { get; }
        public int PointsAwarded { get; }
    }
}
