using System;
using System.Collections.Generic;
using System.Linq;
using EndangeredAR.Animals;
using EndangeredAR.Memory;

namespace EndangeredAR.AI
{
    public static class CharacterMemoryContextFormatter
    {
        private const int MaximumMilestones = 3;
        private const int MaximumDisplayCharacters = 240;

        public static ReadOnlyCharacterMemoryContext Format(
            CharacterMemoryStoreStatus storeStatus,
            CharacterMemoryProjection projection,
            AnimalDefinition definition,
            ReadOnlyCharacterContext currentContext)
        {
            var animalId = definition?.AnimalId ?? currentContext?.Character?.AnimalId ?? string.Empty;
            if (storeStatus == CharacterMemoryStoreStatus.Unavailable ||
                storeStatus == CharacterMemoryStoreStatus.FutureVersion ||
                definition == null ||
                currentContext == null ||
                currentContext.IsEmpty ||
                !string.Equals(currentContext.Character.AnimalId, definition.AnimalId, StringComparison.Ordinal))
            {
                return ReadOnlyCharacterMemoryContext.UnavailableFor(animalId);
            }

            projection ??= CharacterMemoryProjection.Empty;
            var discovered = projection.Discovered && currentContext.Character.Unlocked;
            var completedMissionIds = ResolveMissionIds(projection, definition, currentContext);
            var learnedKnowledgeIds = ResolveKnowledgeIds(projection, definition, currentContext);
            var earnedBadgeCount = Math.Min(
                CountDistinct(projection.EarnedBadgeIds),
                Math.Max(0, currentContext.Character.EarnedBadgeCount));
            var milestones = ResolveMilestones(
                projection,
                definition,
                discovered,
                completedMissionIds,
                learnedKnowledgeIds,
                earnedBadgeCount);

            if (!discovered && completedMissionIds.Count == 0 && learnedKnowledgeIds.Count == 0 && earnedBadgeCount == 0)
            {
                return ReadOnlyCharacterMemoryContext.EmptyFor(definition.AnimalId);
            }

            return ReadOnlyCharacterMemoryContext.Create(
                definition.AnimalId,
                CharacterMemoryContextStatus.Available,
                discovered,
                completedMissionIds.Count,
                learnedKnowledgeIds.Count,
                earnedBadgeCount,
                milestones);
        }

        private static HashSet<string> ResolveMissionIds(
            CharacterMemoryProjection projection,
            AnimalDefinition definition,
            ReadOnlyCharacterContext currentContext)
        {
            var result = new HashSet<string>(StringComparer.Ordinal);
            var mission = definition.Mission;
            if (mission == null ||
                !currentContext.Task.Completed ||
                !string.Equals(currentContext.Task.TaskId, mission.MissionId, StringComparison.Ordinal))
            {
                return result;
            }

            foreach (var value in projection.CompletedMissionIds)
            {
                if (string.Equals(value, mission.MissionId, StringComparison.Ordinal))
                {
                    result.Add(value);
                }
            }

            return result;
        }

        private static HashSet<string> ResolveKnowledgeIds(
            CharacterMemoryProjection projection,
            AnimalDefinition definition,
            ReadOnlyCharacterContext currentContext)
        {
            var validIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (var entry in definition.Knowledge?.Entries ?? Array.Empty<AnimalKnowledgeEntry>())
            {
                if (entry != null && !string.IsNullOrWhiteSpace(entry.KnowledgeId))
                {
                    validIds.Add(entry.KnowledgeId);
                }
            }

            var linkedId = definition.Mission?.LearnedKnowledgeId;
            if (!string.IsNullOrWhiteSpace(linkedId))
            {
                validIds.Add(linkedId);
            }

            var resolved = new HashSet<string>(StringComparer.Ordinal);
            foreach (var value in projection.LearnedKnowledgeIds)
            {
                if (!string.IsNullOrWhiteSpace(value) && validIds.Contains(value))
                {
                    resolved.Add(value);
                }
            }

            var allowedCount = Math.Max(0, currentContext.Character.LearnedKnowledgeCount);
            if (resolved.Count <= allowedCount)
            {
                return resolved;
            }

            return new HashSet<string>(resolved.OrderBy(value => value, StringComparer.Ordinal).Take(allowedCount), StringComparer.Ordinal);
        }

        private static IReadOnlyList<ReadOnlyCharacterMemoryMilestone> ResolveMilestones(
            CharacterMemoryProjection projection,
            AnimalDefinition definition,
            bool discovered,
            HashSet<string> completedMissionIds,
            HashSet<string> learnedKnowledgeIds,
            int earnedBadgeCount)
        {
            var result = new List<ReadOnlyCharacterMemoryMilestone>();
            var seenKinds = new HashSet<CharacterMemoryContextMilestoneKind>();
            var characterCount = 0;
            foreach (var milestone in projection.RecentMilestones)
            {
                if (result.Count >= MaximumMilestones || !TryResolveMilestone(
                        milestone,
                        definition,
                        discovered,
                        completedMissionIds,
                        learnedKnowledgeIds,
                        earnedBadgeCount,
                        out var resolved))
                {
                    continue;
                }

                if (!seenKinds.Add(resolved.Kind) || characterCount + resolved.DisplayLabel.Length > MaximumDisplayCharacters)
                {
                    continue;
                }

                result.Add(resolved);
                characterCount += resolved.DisplayLabel.Length;
            }

            return result;
        }

        private static bool TryResolveMilestone(
            CharacterMemoryMilestone milestone,
            AnimalDefinition definition,
            bool discovered,
            HashSet<string> completedMissionIds,
            HashSet<string> learnedKnowledgeIds,
            int earnedBadgeCount,
            out ReadOnlyCharacterMemoryMilestone resolved)
        {
            resolved = null;
            switch (milestone.EventType)
            {
                case CharacterMemoryEventType.AnimalDiscovered:
                    if (discovered && string.Equals(milestone.SubjectId, definition.AnimalId, StringComparison.Ordinal))
                    {
                        return TryCreateMilestone(
                            CharacterMemoryContextMilestoneKind.AnimalDiscovered,
                            definition.DisplayName,
                            out resolved);
                    }

                    return false;
                case CharacterMemoryEventType.MissionCompleted:
                    if (definition.Mission != null && completedMissionIds.Contains(milestone.SubjectId))
                    {
                        return TryCreateMilestone(
                            CharacterMemoryContextMilestoneKind.MissionCompleted,
                            definition.Mission.Title,
                            out resolved);
                    }

                    return false;
                case CharacterMemoryEventType.KnowledgeLearned:
                    if (!learnedKnowledgeIds.Contains(milestone.SubjectId))
                    {
                        return false;
                    }

                    return TryCreateMilestone(
                        CharacterMemoryContextMilestoneKind.KnowledgeLearned,
                        ResolveKnowledgeLabel(definition, milestone.SubjectId),
                        out resolved);
                case CharacterMemoryEventType.BadgeEarned:
                    return false;
                default:
                    return false;
            }
        }

        private static string ResolveKnowledgeLabel(AnimalDefinition definition, string knowledgeId)
        {
            foreach (var entry in definition.Knowledge?.Entries ?? Array.Empty<AnimalKnowledgeEntry>())
            {
                if (entry != null && string.Equals(entry.KnowledgeId, knowledgeId, StringComparison.Ordinal))
                {
                    var topicLabel = ResolveTopicLabel(entry.Topic);
                    return string.IsNullOrEmpty(topicLabel)
                        ? string.Empty
                        : $"{definition.ShortName}的{topicLabel}";
                }
            }

            if (definition.Mission != null &&
                string.Equals(definition.Mission.LearnedKnowledgeId, knowledgeId, StringComparison.Ordinal) &&
                !string.IsNullOrWhiteSpace(definition.Mission.Title))
            {
                return $"“{definition.Mission.Title.Trim()}”任务中的相关知识";
            }

            return string.Empty;
        }

        private static string ResolveTopicLabel(string topic)
        {
            switch (topic)
            {
                case "identity": return "身份知识";
                case "scientific_name": return "学名知识";
                case "diet": return "食性知识";
                case "range": return "分布知识";
                case "habitat": return "栖息地知识";
                case "behavior": return "行为知识";
                case "threats": return "生存威胁知识";
                case "population": return "种群知识";
                case "conservation_status": return "保护状态知识";
                case "conservation_actions": return "保护行动知识";
                case "youth_actions": return "青少年保护行动知识";
                default: return string.Empty;
            }
        }

        private static bool TryCreateMilestone(
            CharacterMemoryContextMilestoneKind kind,
            string label,
            out ReadOnlyCharacterMemoryMilestone milestone)
        {
            milestone = null;
            if (string.IsNullOrWhiteSpace(label))
            {
                return false;
            }

            milestone = new ReadOnlyCharacterMemoryMilestone(kind, label.Trim());
            return true;
        }

        private static int CountDistinct(IEnumerable<string> values)
        {
            if (values == null)
            {
                return 0;
            }

            var distinct = new HashSet<string>(StringComparer.Ordinal);
            foreach (var value in values)
            {
                if (!string.IsNullOrWhiteSpace(value))
                {
                    distinct.Add(value);
                }
            }

            return distinct.Count;
        }
    }
}
