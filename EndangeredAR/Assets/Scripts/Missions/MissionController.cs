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

        public string CurrentMissionId => definition == null ? string.Empty : definition.MissionId;
        public int Points => points;
        public MissionState State => state;
        public bool IsCompleted => state == MissionState.Completed;

        public void Configure(MissionDefinition configuredDefinition, bool alreadyCompleted = false)
        {
            definition = configuredDefinition;
            points = 0;
            state = definition != null && alreadyCompleted
                ? MissionState.Completed
                : MissionState.NotStarted;
        }

        public void StartMission()
        {
            if (definition == null || state == MissionState.Completed)
            {
                return;
            }

            state = MissionState.Choosing;
        }

        public void StartFoodMission()
        {
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
            points += definition.Points;
            return CreateResult(true, definition.CorrectFeedback, definition.Points);
        }

        public MissionResult SelectFood(string option)
        {
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
