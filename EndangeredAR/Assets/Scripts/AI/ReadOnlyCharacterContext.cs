using System;
using System.Collections.Generic;
using UnityEngine;

namespace EndangeredAR.AI
{
    [Serializable]
    public sealed class ReadOnlyCharacterContext
    {
        [SerializeField] private ReadOnlyCharacterState character;
        [SerializeField] private ReadOnlyTaskState task;
        [SerializeField] private ReadOnlyInteractionState interaction;

        private ReadOnlyCharacterContext(
            ReadOnlyCharacterState character,
            ReadOnlyTaskState task,
            ReadOnlyInteractionState interaction)
        {
            this.character = character ?? ReadOnlyCharacterState.Empty;
            this.task = task ?? ReadOnlyTaskState.Empty;
            this.interaction = interaction ?? ReadOnlyInteractionState.Empty;
        }

        public ReadOnlyCharacterState Character => character ?? ReadOnlyCharacterState.Empty;
        public ReadOnlyTaskState Task => task ?? ReadOnlyTaskState.Empty;
        public ReadOnlyInteractionState Interaction => interaction ?? ReadOnlyInteractionState.Empty;
        public bool IsEmpty => string.IsNullOrEmpty(Character.AnimalId);

        public static ReadOnlyCharacterContext Empty => Create(
            ReadOnlyCharacterState.Empty,
            ReadOnlyTaskState.Empty,
            ReadOnlyInteractionState.Empty);

        internal static ReadOnlyCharacterContext Create(
            ReadOnlyCharacterState character,
            ReadOnlyTaskState task,
            ReadOnlyInteractionState interaction)
        {
            return new ReadOnlyCharacterContext(character, task, interaction);
        }
    }

    [Serializable]
    public sealed class ReadOnlyCharacterState
    {
        [SerializeField] private string animalId;
        [SerializeField] private bool unlocked;
        [SerializeField] private int learnedKnowledgeCount;
        [SerializeField] private int earnedBadgeCount;

        internal ReadOnlyCharacterState(
            string animalId,
            bool unlocked,
            int learnedKnowledgeCount,
            int earnedBadgeCount)
        {
            this.animalId = animalId ?? string.Empty;
            this.unlocked = unlocked;
            this.learnedKnowledgeCount = Math.Max(0, learnedKnowledgeCount);
            this.earnedBadgeCount = Math.Max(0, earnedBadgeCount);
        }

        public string AnimalId => animalId ?? string.Empty;
        public bool Unlocked => unlocked;
        public int LearnedKnowledgeCount => learnedKnowledgeCount;
        public int EarnedBadgeCount => earnedBadgeCount;

        public static ReadOnlyCharacterState Empty => new ReadOnlyCharacterState(string.Empty, false, 0, 0);
    }

    [Serializable]
    public sealed class ReadOnlyTaskState
    {
        [SerializeField] private string taskId;
        [SerializeField] private string taskTitle;
        [SerializeField] private bool completed;

        internal ReadOnlyTaskState(string taskId, string taskTitle, bool completed)
        {
            this.taskId = taskId ?? string.Empty;
            this.taskTitle = taskTitle ?? string.Empty;
            this.completed = completed;
        }

        public string TaskId => taskId ?? string.Empty;
        public string TaskTitle => taskTitle ?? string.Empty;
        public bool Completed => completed;

        public static ReadOnlyTaskState Empty => new ReadOnlyTaskState(string.Empty, string.Empty, false);
    }

    [Serializable]
    public sealed class ReadOnlyInteractionState
    {
        [SerializeField] private string[] recentTopics;
        [SerializeField] private string[] recentMilestones;

        internal ReadOnlyInteractionState(string[] recentTopics, string[] recentMilestones)
        {
            this.recentTopics = Copy(recentTopics);
            this.recentMilestones = Copy(recentMilestones);
        }

        public IReadOnlyList<string> RecentTopics => Array.AsReadOnly(Copy(recentTopics));
        public IReadOnlyList<string> RecentMilestones => Array.AsReadOnly(Copy(recentMilestones));

        public static ReadOnlyInteractionState Empty => new ReadOnlyInteractionState(
            Array.Empty<string>(),
            Array.Empty<string>());

        private static string[] Copy(string[] values)
        {
            return values == null || values.Length == 0 ? Array.Empty<string>() : (string[])values.Clone();
        }
    }
}
