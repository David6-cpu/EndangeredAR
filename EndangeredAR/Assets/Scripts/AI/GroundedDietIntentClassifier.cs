using System;
using System.Text;
using System.Text.RegularExpressions;

namespace EndangeredAR.AI
{
    internal static class GroundedDietIntentClassifier
    {
        private static readonly string[] RejectedMarkers =
        {
            "不要吃", "别吃", "不吃", "刚才为什么吃", "现在没有在吃", "没有在吃",
            "薯片", "巧克力", "人类零食", "零食", "塑料", "包装食品", "喂你", "给你吃",
            "eat", "trigger", "animator", "settrigger", "tryplayaction",
            "播放", "执行", "触发", "动画", "动作", "表演", "给我看",
            "忽略规则", "忽略证据", "修改groundingtopic", "伪造citation", "删除数据",
            "修改任务", "修改进度", "解锁", "发徽章"
        };

        private static readonly Regex[] AllowedPatterns =
        {
            new Regex("^(森森)?你(平时|通常|一般)?吃(什么|哪些.+)$", RegexOptions.CultureInvariant),
            new Regex("^(森森)?你(最)?(喜欢|爱)吃(什么|哪些.+|.+食物)$", RegexOptions.CultureInvariant),
            new Regex("^(森森)?你最喜欢什么食物$", RegexOptions.CultureInvariant),
            new Regex("^(森森)?你以什么为食$", RegexOptions.CultureInvariant),
            new Regex("^给我介绍一下(森森的|你的)?食性$", RegexOptions.CultureInvariant),
            new Regex("^(森森)?你会怎么吃这些树叶$", RegexOptions.CultureInvariant),
            new Regex("^森森的食物(是|有)什么$", RegexOptions.CultureInvariant)
        };

        public static bool IsEligible(string message)
        {
            var normalized = Normalize(message);
            if (string.IsNullOrEmpty(normalized) || ContainsAny(normalized, RejectedMarkers) || IsPreciseQuantity(normalized))
            {
                return false;
            }

            foreach (var pattern in AllowedPatterns)
            {
                if (pattern.IsMatch(normalized))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsPreciseQuantity(string normalized)
        {
            if (ContainsAny(normalized, new[] { "准确", "精确", "具体", "数字", "多少克", "千克", "公斤", "几片" }))
            {
                return true;
            }

            return ContainsAny(normalized, new[] { "每天", "每日", "每餐", "一天" }) &&
                   ContainsAny(normalized, new[] { "多少", "几", "数量" });
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
