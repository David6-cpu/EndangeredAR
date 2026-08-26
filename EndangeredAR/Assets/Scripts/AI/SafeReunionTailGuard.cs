using System;

namespace EndangeredAR.AI
{
    public static class SafeReunionTailGuard
    {
        public const string FallbackTail = "很高兴又见到你！";
        private const int MaximumCharacters = 24;

        private static readonly string[] RejectedMarkers =
        {
            "任务", "知识", "徽章", "记得", "以前", "此前", "曾经", "已经",
            "刚刚", "昨天", "上周", "上次", "最近", "第一次", "日期", "时间",
            "学名", "食性", "分布", "保护等级", "聊天", "系统", "指令", "prompt",
            "animator", "trigger", "eat", "taunt", "执行", "播放", "修改", "删除"
        };

        public static bool TryAccept(string candidate, out string accepted)
        {
            accepted = string.Empty;
            if (string.IsNullOrWhiteSpace(candidate))
            {
                return false;
            }

            var trimmed = candidate.Trim();
            if (trimmed.Length > MaximumCharacters)
            {
                return false;
            }

            foreach (var character in trimmed)
            {
                if (char.IsDigit(character))
                {
                    return false;
                }
            }

            foreach (var marker in RejectedMarkers)
            {
                if (trimmed.IndexOf(marker, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return false;
                }
            }

            accepted = trimmed;
            return true;
        }
    }
}
