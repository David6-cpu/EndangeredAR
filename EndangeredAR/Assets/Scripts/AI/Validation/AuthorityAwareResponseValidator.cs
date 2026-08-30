using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using EndangeredAR.AI.Knowledge;

namespace EndangeredAR.AI.Validation
{
    public static class AuthorityAwareResponseValidator
    {
        private const int MaximumReplyCharacters = 320;
        private static readonly Regex NumberPattern = new Regex(@"\d+(?:\.\d+)?", RegexOptions.CultureInvariant);
        private static readonly Regex CountPattern = new Regex(@"[零一二三四五六七八九十百]+(?:项|枚|个)", RegexOptions.CultureInvariant);
        private static readonly Regex LatinBinomialPattern = new Regex(@"\b[A-Z][a-z]+\s+[a-z]{3,}\b", RegexOptions.CultureInvariant);
        private static readonly Regex QuotedLabelPattern = new Regex("[“\"]([^”\"]+)[”\"]", RegexOptions.CultureInvariant);

        public static AIResponseValidationResult Validate(
            AIRequest request,
            CanonicalEvidencePackage evidence,
            string reply)
        {
            if (request == null || string.IsNullOrWhiteSpace(reply) ||
                reply.Trim().Length > MaximumReplyCharacters || ContainsTechnicalText(reply))
            {
                return AIResponseValidationResult.Reject("invalid_response_shape");
            }

            var text = reply.Trim();
            switch (request.ContentAuthority)
            {
                case ContentAuthority.CanonicalKnowledge:
                    return ValidateCanonical(evidence, text);
                case ContentAuthority.CurrentProgress:
                    return ValidateCurrentProgress(request.Context, text);
                case ContentAuthority.CharacterMemory:
                    return ValidateMemory(request.MemoryContext, text);
                case ContentAuthority.SystemPolicy:
                    return request.MemoryUseMode == MemoryUseMode.HistoryBoundary
                        ? ValidateHistoryBoundary(text)
                        : AIResponseValidationResult.Valid;
                case ContentAuthority.None:
                default:
                    return ContainsUnauthorizedScientificClaim(text)
                        ? AIResponseValidationResult.Reject("unauthorized_scientific_claim")
                        : AIResponseValidationResult.Valid;
            }
        }

        private static AIResponseValidationResult ValidateCanonical(
            CanonicalEvidencePackage evidence,
            string reply)
        {
            if (evidence == null || evidence.AnswerMode != "grounded_fact" ||
                string.IsNullOrWhiteSpace(evidence.ApprovedAnswerConstraint))
            {
                return AIResponseValidationResult.Reject("canonical_evidence_missing");
            }

            var authority = BuildCanonicalAuthority(evidence);
            if (HasUnauthorizedNumbers(reply, authority) || HasUnauthorizedLatinName(reply, authority))
            {
                return AIResponseValidationResult.Reject("canonical_fact_conflict");
            }

            if (ContainsAny(reply, "根据", "来源于", "研究显示", "报告指出", "参考资料"))
            {
                return AIResponseValidationResult.Reject("unsupported_source_claim");
            }

            if (evidence.Facts.Any(fact => fact.Topic == "diet"))
            {
                if (ContainsAny(reply, "有毒", "致死", "疾病", "生病") ||
                    ContainsUnsafePositiveFoodClaim(reply))
                {
                    return AIResponseValidationResult.Reject("unsupported_food_safety_claim");
                }

                var dietItems = evidence.Facts.SelectMany(fact => fact.Items)
                    .Where(value => !string.IsNullOrWhiteSpace(value));
                return dietItems.Any(item => reply.Contains(item))
                    ? AIResponseValidationResult.Valid
                    : AIResponseValidationResult.Reject("canonical_anchor_missing");
            }

            if (evidence.Facts.Any(fact => fact.Topic == "scientific_name"))
            {
                var scientificName = evidence.Facts
                    .Select(fact => fact.DisplayValue)
                    .FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));
                return !string.IsNullOrEmpty(scientificName) && reply.Contains(scientificName)
                    ? AIResponseValidationResult.Valid
                    : AIResponseValidationResult.Reject("canonical_anchor_missing");
            }

            if (evidence.EvidenceStatus == "insufficient_evidence")
            {
                return ContainsAny(reply, "没有可靠", "缺少可靠", "无法确定", "不能编", "不知道")
                    ? AIResponseValidationResult.Valid
                    : AIResponseValidationResult.Reject("insufficient_evidence_claim_missing");
            }

            var anchors = evidence.Facts
                .SelectMany(fact => fact.Items.Concat(new[] { fact.DisplayValue }))
                .Where(value => !string.IsNullOrWhiteSpace(value));
            return anchors.Any(anchor => ContainsUsefulAnchor(reply, anchor))
                ? AIResponseValidationResult.Valid
                : AIResponseValidationResult.Reject("canonical_anchor_missing");
        }

        private static AIResponseValidationResult ValidateCurrentProgress(
            ReadOnlyCharacterContext context,
            string reply)
        {
            context ??= ReadOnlyCharacterContext.Empty;
            var task = context.Task;
            if (string.IsNullOrEmpty(task.TaskTitle))
            {
                if (!ContainsAny(reply, "没有提供任务", "没有当前任务", "暂无任务"))
                {
                    return AIResponseValidationResult.Reject("current_task_missing");
                }
            }
            else if (!ContainsTaskAnchor(reply, task.TaskTitle))
            {
                return AIResponseValidationResult.Reject("current_task_missing");
            }

            if (task.Completed &&
                !ContainsAny(reply, "已经完成", "已完成", "完成了", "任务完成"))
            {
                return AIResponseValidationResult.Reject("current_task_state_missing");
            }

            if ((!task.Completed && ContainsAny(reply, "已经完成", "任务完成了", "已完成任务")) ||
                (task.Completed && ContainsAny(reply, "尚未完成", "还没完成", "未完成任务")))
            {
                return AIResponseValidationResult.Reject("current_task_state_conflict");
            }

            if (task.Completed && ContainsAny(
                    reply,
                    "建议",
                    "可以去",
                    "可以再",
                    "可以先",
                    "可以继续",
                    "继续完成",
                    "重新",
                    "再次挑战",
                    "去找",
                    "去寻找",
                    "寻找一些",
                    "比如",
                    "例如",
                    "尝试"))
            {
                return AIResponseValidationResult.Reject("current_task_guidance_not_authorized");
            }

            var authority = string.Join("|", new[]
            {
                context.Character.LearnedKnowledgeCount.ToString(),
                context.Character.EarnedBadgeCount.ToString()
            });
            return HasUnauthorizedNumbers(reply, authority)
                ? AIResponseValidationResult.Reject("current_state_count_conflict")
                : AIResponseValidationResult.Valid;
        }

        private static AIResponseValidationResult ValidateMemory(
            ReadOnlyCharacterMemoryContext context,
            string reply)
        {
            if (ContainsAny(reply, "昨天", "上周", "上次", "刚刚", "最近", "第一次", "具体日期") ||
                ContainsAny(reply, "你之前问过", "我记得我们聊过", "你曾经跟我说过"))
            {
                return AIResponseValidationResult.Reject("memory_claim_not_authorized");
            }

            context ??= ReadOnlyCharacterMemoryContext.UnavailableFor(string.Empty);
            var authority = BuildMemoryAuthority(context);
            if (HasUnauthorizedNumbers(reply, authority) || HasUnauthorizedCounts(reply, authority) ||
                HasUnauthorizedQuotedLabels(reply, authority))
            {
                return AIResponseValidationResult.Reject("memory_claim_not_authorized");
            }

            switch (context.Status)
            {
                case CharacterMemoryContextStatus.Available:
                    var anchors = context.Milestones
                        .Where(value => value != null && !string.IsNullOrWhiteSpace(value.DisplayLabel))
                        .Select(value => value.DisplayLabel)
                        .Concat(new[] { "保护任务", "知识", "徽章", "发现" });
                    return anchors.Any(reply.Contains)
                        ? AIResponseValidationResult.Valid
                        : AIResponseValidationResult.Reject("memory_anchor_missing");
                case CharacterMemoryContextStatus.Empty:
                    return ContainsAny(reply, "没有", "暂时没有", "未保存")
                        ? AIResponseValidationResult.Valid
                        : AIResponseValidationResult.Reject("memory_empty_claim_missing");
                case CharacterMemoryContextStatus.Unavailable:
                default:
                    return ContainsAny(reply, "无法读取", "读取不到", "暂时不能读取")
                        ? AIResponseValidationResult.Valid
                        : AIResponseValidationResult.Reject("memory_unavailable_claim_missing");
            }
        }

        private static AIResponseValidationResult ValidateHistoryBoundary(string reply)
        {
            if (ContainsAny(
                    reply,
                    "你之前问过",
                    "我记得你",
                    "我还记得",
                    "我记得我们聊过",
                    "你曾经跟我说过",
                    "你提过",
                    "我们聊过",
                    "我们讨论过",
                    "之前讨论过",
                    "我忘记",
                    "实时更新",
                    "最近的记忆"))
            {
                return AIResponseValidationResult.Reject("chat_history_claim_not_authorized");
            }

            return ContainsAny(reply, "聊天", "对话") &&
                   ContainsAny(reply, "不保存", "没有保存", "不会长期保存", "未保存")
                ? AIResponseValidationResult.Valid
                : AIResponseValidationResult.Reject("history_boundary_missing");
        }

        private static string BuildCanonicalAuthority(CanonicalEvidencePackage evidence)
        {
            return string.Join("|", evidence.Facts.SelectMany(fact =>
                fact.Items.Concat(new[]
                {
                    fact.Claim,
                    fact.ApprovedAnswer,
                    fact.DisplayValue
                })));
        }

        private static string BuildMemoryAuthority(ReadOnlyCharacterMemoryContext context)
        {
            var authority = CharacterMemoryAnswerBuilder.BuildExplicitRecall(context);
            return string.Join("|", new[]
            {
                authority,
                CountAliases(context.CompletedMissionCount, "项"),
                CountAliases(context.LearnedKnowledgeCount, "个"),
                CountAliases(context.EarnedBadgeCount, "枚")
            });
        }

        private static string CountAliases(int value, string unit)
        {
            var chinese = value switch
            {
                0 => "零",
                1 => "一",
                2 => "二",
                3 => "三",
                4 => "四",
                5 => "五",
                6 => "六",
                7 => "七",
                8 => "八",
                9 => "九",
                10 => "十",
                _ => string.Empty
            };
            return string.IsNullOrEmpty(chinese)
                ? value + unit
                : value + unit + "|" + chinese + unit;
        }

        private static bool HasUnauthorizedNumbers(string reply, string authority)
        {
            foreach (Match match in NumberPattern.Matches(reply))
            {
                if (!authority.Contains(match.Value))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool HasUnauthorizedCounts(string reply, string authority)
        {
            foreach (Match match in CountPattern.Matches(reply))
            {
                if (!authority.Contains(match.Value))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool HasUnauthorizedLatinName(string reply, string authority)
        {
            foreach (Match match in LatinBinomialPattern.Matches(reply))
            {
                if (!authority.Contains(match.Value))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool HasUnauthorizedQuotedLabels(string reply, string authority)
        {
            foreach (Match match in QuotedLabelPattern.Matches(reply))
            {
                if (!authority.Contains(match.Groups[1].Value))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool ContainsUnsafePositiveFoodClaim(string reply)
        {
            return ContainsAny(reply, "薯片", "巧克力", "人类零食", "塑料", "包装食品") &&
                   ContainsAny(reply, "可以吃", "能吃", "适合", "正常食物");
        }

        private static bool ContainsUnauthorizedScientificClaim(string reply)
        {
            return LatinBinomialPattern.IsMatch(reply) ||
                   ContainsAny(reply, "学名是", "IUCN", "CITES", "保护等级", "濒危等级", "分布在");
        }

        private static bool ContainsTechnicalText(string reply)
        {
            return ContainsAny(
                reply,
                "Animator.SetTrigger",
                "TryPlayAction",
                "groundingTopic",
                "memoryUpdate",
                "system prompt",
                "系统提示词",
                "http://",
                "https://");
        }

        private static bool ContainsUsefulAnchor(string reply, string anchor)
        {
            if (reply.Contains(anchor))
            {
                return true;
            }

            foreach (var segment in anchor.Split('；', '，', '、', '：', ',', ';'))
            {
                var value = segment.Trim();
                if (value.Length >= 2 && reply.Contains(value))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool ContainsTaskAnchor(string reply, string taskTitle)
        {
            if (reply.Contains(taskTitle))
            {
                return true;
            }

            const int window = 4;
            for (var index = 0; index <= taskTitle.Length - window; index++)
            {
                if (reply.Contains(taskTitle.Substring(index, window)))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool ContainsAny(string value, params string[] markers)
        {
            return markers.Any(marker => value.IndexOf(marker, StringComparison.OrdinalIgnoreCase) >= 0);
        }
    }
}
