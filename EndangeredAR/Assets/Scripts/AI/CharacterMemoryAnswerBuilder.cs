using System;
using System.Collections.Generic;

namespace EndangeredAR.AI
{
    public static class CharacterMemoryAnswerBuilder
    {
        public const string MemoryRecallAnswerMode = "memory_recall";
        public const int MaximumTextCharacters = 240;
        private const int MaximumClaims = 4;

        public static string BuildExplicitRecall(ReadOnlyCharacterMemoryContext context)
        {
            if (context == null || context.Status == CharacterMemoryContextStatus.Unavailable)
            {
                return "我现在暂时无法读取长期记忆记录。";
            }

            if (context.Status != CharacterMemoryContextStatus.Available)
            {
                return "我目前没有保存到可用于长期回忆的里程碑记录。";
            }

            var claims = BuildClaims(context, MaximumClaims);
            var bounded = BuildBoundedClaimList(claims);
            return string.IsNullOrEmpty(bounded)
                ? "我目前没有保存到可用于长期回忆的里程碑记录。"
                : bounded;
        }

        public static string BuildConversationHistoryBoundary()
        {
            return "长期里程碑记忆不保存完整聊天内容；最近聊天记录与长期里程碑记忆是不同来源，所以我不能编造你以前问过的话题。";
        }

        public static string BuildReunion(
            ReadOnlyCharacterMemoryContext context,
            string proposedTail)
        {
            if (context == null || context.Status == CharacterMemoryContextStatus.Unavailable)
            {
                return "很高兴见到你！不过我现在暂时无法读取长期记忆记录。";
            }

            if (context.Status != CharacterMemoryContextStatus.Available)
            {
                return "很高兴见到你！";
            }

            var claims = BuildClaims(context, 1);
            if (claims.Count == 0)
            {
                return "很高兴见到你！";
            }

            var tail = SafeReunionTailGuard.TryAccept(proposedTail, out var accepted)
                ? accepted
                : SafeReunionTailGuard.FallbackTail;
            var reply = $"欢迎回来！{claims[0]}。{tail}";
            return reply.Length <= MaximumTextCharacters ? reply : "很高兴见到你！";
        }

        private static string BuildBoundedClaimList(IReadOnlyList<string> claims)
        {
            const string prefix = "我保存到的长期里程碑有：";
            const string separator = "；";
            const string suffix = "。";
            var accepted = new List<string>();
            var length = prefix.Length + suffix.Length;
            foreach (var claim in claims)
            {
                var additional = claim.Length + (accepted.Count == 0 ? 0 : separator.Length);
                if (length + additional > MaximumTextCharacters)
                {
                    continue;
                }

                accepted.Add(claim);
                length += additional;
            }

            return accepted.Count == 0
                ? string.Empty
                : prefix + string.Join(separator, accepted) + suffix;
        }

        private static IReadOnlyList<string> BuildClaims(
            ReadOnlyCharacterMemoryContext context,
            int maximumClaims)
        {
            var claims = new List<string>();
            var representedKinds = new HashSet<CharacterMemoryContextMilestoneKind>();
            foreach (var milestone in context.Milestones)
            {
                if (claims.Count >= maximumClaims || milestone == null ||
                    string.IsNullOrWhiteSpace(milestone.DisplayLabel) ||
                    !representedKinds.Add(milestone.Kind))
                {
                    continue;
                }

                var claim = FormatMilestone(milestone);
                if (!string.IsNullOrEmpty(claim))
                {
                    claims.Add(claim);
                }
            }

            AddAggregate(
                claims,
                representedKinds,
                CharacterMemoryContextMilestoneKind.MissionCompleted,
                context.CompletedMissionCount,
                count => $"你以前完成过{count}项保护任务",
                maximumClaims);
            AddAggregate(
                claims,
                representedKinds,
                CharacterMemoryContextMilestoneKind.KnowledgeLearned,
                context.LearnedKnowledgeCount,
                count => $"你以前学习过{count}个知识主题",
                maximumClaims);
            AddAggregate(
                claims,
                representedKinds,
                CharacterMemoryContextMilestoneKind.BadgeEarned,
                context.EarnedBadgeCount,
                count => $"你以前获得过{count}枚相关徽章",
                maximumClaims);
            if (claims.Count < maximumClaims && context.Discovered &&
                representedKinds.Add(CharacterMemoryContextMilestoneKind.AnimalDiscovered))
            {
                claims.Add("你以前已经发现过当前动物");
            }

            return claims;
        }

        private static string FormatMilestone(ReadOnlyCharacterMemoryMilestone milestone)
        {
            switch (milestone.Kind)
            {
                case CharacterMemoryContextMilestoneKind.AnimalDiscovered:
                    return $"你以前已经发现过{milestone.DisplayLabel}";
                case CharacterMemoryContextMilestoneKind.MissionCompleted:
                    return $"你此前完成过“{milestone.DisplayLabel}”";
                case CharacterMemoryContextMilestoneKind.KnowledgeLearned:
                    return $"你以前学习过{milestone.DisplayLabel}";
                case CharacterMemoryContextMilestoneKind.BadgeEarned:
                    return $"你以前获得过“{milestone.DisplayLabel}”";
                default:
                    return string.Empty;
            }
        }

        private static void AddAggregate(
            ICollection<string> claims,
            ISet<CharacterMemoryContextMilestoneKind> representedKinds,
            CharacterMemoryContextMilestoneKind kind,
            int count,
            Func<int, string> formatter,
            int maximumClaims)
        {
            if (claims.Count >= maximumClaims || count <= 0 || !representedKinds.Add(kind))
            {
                return;
            }

            claims.Add(formatter(count));
        }
    }
}
