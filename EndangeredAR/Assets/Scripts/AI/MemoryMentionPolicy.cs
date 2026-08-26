using System;
using System.Text;

namespace EndangeredAR.AI
{
    public enum MemoryMentionMode
    {
        None,
        ExplicitRecall,
        ConversationHistoryBoundary,
        Reunion
    }

    public static class MemoryMentionModeProtocol
    {
        public static bool TryParseExact(string wireValue, out MemoryMentionMode mode)
        {
            switch (wireValue)
            {
                case "none":
                    mode = MemoryMentionMode.None;
                    return true;
                case "explicit_recall":
                    mode = MemoryMentionMode.ExplicitRecall;
                    return true;
                case "conversation_history_boundary":
                    mode = MemoryMentionMode.ConversationHistoryBoundary;
                    return true;
                case "reunion":
                    mode = MemoryMentionMode.Reunion;
                    return true;
                default:
                    mode = default;
                    return false;
            }
        }

        public static string ToWireValue(MemoryMentionMode mode)
        {
            switch (mode)
            {
                case MemoryMentionMode.None:
                    return "none";
                case MemoryMentionMode.ExplicitRecall:
                    return "explicit_recall";
                case MemoryMentionMode.ConversationHistoryBoundary:
                    return "conversation_history_boundary";
                case MemoryMentionMode.Reunion:
                    return "reunion";
                default:
                    return string.Empty;
            }
        }
    }

    public static class MemoryMentionPolicy
    {
        private static readonly string[] RejectedMarkers =
        {
            "忽略规则", "忽略projection", "忽略memory", "声称我完成所有任务",
            "修改memorystatus", "修改memorycontext", "memoryupdate", "写入记忆",
            "记住我的名字", "记住我的邮箱", "邮箱", "email", "设备id", "用户id",
            "profilekey", "eventid", "idempotencykey", "systemprompt", "系统指令",
            "修改任务", "修改进度", "解锁", "发徽章", "animator", "settrigger",
            "tryplayaction", "从memory触发", "从记忆触发"
        };

        private static readonly string[] ConversationHistoryMarkers =
        {
            "以前问过", "以前聊过", "之前问过", "之前聊过", "保存了我们的聊天",
            "保存我们的聊天", "保存了聊天", "所有聊天", "完整聊天", "聊天原文"
        };

        private static readonly string[] ExplicitRecallMarkers =
        {
            "还记得我吗", "记得我吗", "你还记得我做过什么", "你还记得我以前做过什么",
            "你记得我做过什么", "我以前做过什么", "我之前做过什么", "我都做过什么",
            "我完成过任务吗", "我以前完成过任务吗", "我以前学过什么",
            "我之前学过什么", "我获得过徽章吗", "我以前帮助过你吗",
            "我之前帮助过你吗", "我以前帮过你什么", "我之前帮过你什么",
            "我们以前完成过什么", "我们之前做过什么"
        };

        private static readonly string[] ReunionMarkers =
        {
            "我回来了", "我又来看你了", "好久不见", "我们又见面了"
        };

        public static MemoryMentionMode Classify(string message)
        {
            var normalized = Normalize(message);
            if (string.IsNullOrEmpty(normalized) || ContainsAny(normalized, RejectedMarkers))
            {
                return MemoryMentionMode.None;
            }

            if (ContainsAny(normalized, ConversationHistoryMarkers))
            {
                return MemoryMentionMode.ConversationHistoryBoundary;
            }

            if (ContainsAny(normalized, ExplicitRecallMarkers))
            {
                return MemoryMentionMode.ExplicitRecall;
            }

            if (ContainsAny(normalized, ReunionMarkers))
            {
                return MemoryMentionMode.Reunion;
            }

            return MemoryMentionMode.None;
        }

        private static bool ContainsAny(string normalized, string[] markers)
        {
            foreach (var marker in markers)
            {
                if (normalized.IndexOf(Normalize(marker), StringComparison.Ordinal) >= 0)
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
    }
}
