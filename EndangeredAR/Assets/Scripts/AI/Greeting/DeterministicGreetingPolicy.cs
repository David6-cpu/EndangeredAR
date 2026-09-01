using System;
using System.Text;

namespace EndangeredAR.AI
{
    public enum GreetingIntentReasonCode
    {
        NotGreeting,
        DirectGreeting,
        TimeOfDayGreeting,
        InformalGreeting,
        MeetingGreeting,
        ReunionGreeting,
        Negated,
        QuotedSpeech,
        DefinitionOrExplanation,
        TechnicalContext,
        PromptInjection
    }

    public readonly struct GreetingIntentResult
    {
        public GreetingIntentResult(
            bool isGreeting,
            GreetingIntentReasonCode reasonCode,
            string policyVersion)
        {
            IsGreeting = isGreeting;
            ReasonCode = reasonCode;
            PolicyVersion = policyVersion ?? string.Empty;
        }

        public bool IsGreeting { get; }
        public GreetingIntentReasonCode ReasonCode { get; }
        public string PolicyVersion { get; }
    }

    public static class DeterministicGreetingPolicy
    {
        public const string PolicyVersion = "r3.4a5-greeting-intent-v1";

        private static readonly string[] PromptInjectionFragments =
        {
            "忽略规则",
            "绕过规则",
            "强制执行",
            "假装这是问候",
            "修改分类结果",
            "返回greeting",
            "执行wave"
        };

        private static readonly string[] TechnicalFragments =
        {
            "animator",
            "settrigger",
            "wave动画",
            "greetingintentresult",
            "问候识别器",
            "分类为greeting",
            "输出greeting",
            "测试问候"
        };

        private static readonly string[] NegationFragments =
        {
            "不要问好",
            "不要跟我打招呼",
            "不要挥手",
            "别说你好",
            "不是来打招呼",
            "不用问好",
            "不许问好"
        };

        private static readonly string[] ReportedSpeechFragments =
        {
            "他对我说",
            "她对我说",
            "他说",
            "她说",
            "老师让大家说",
            "这句话里",
            "翻译成"
        };

        private static readonly string[] ExplanationFragments =
        {
            "是什么意思",
            "什么含义",
            "请解释",
            "为什么人们",
            "有什么区别",
            "greeting是什么",
            "问候是什么",
            "你好的地方是什么"
        };

        private static readonly string[] DirectGreetings =
        {
            "你好",
            "您好"
        };

        private static readonly string[] TimeOfDayGreetings =
        {
            "早上好",
            "上午好",
            "下午好",
            "晚上好",
            "早安"
        };

        private static readonly string[] InformalGreetings =
        {
            "嗨",
            "哈喽",
            "哈啰",
            "hello",
            "hi"
        };

        private static readonly string[] MeetingGreetings =
        {
            "很高兴见到你",
            "初次见面",
            "见到你真好"
        };

        private static readonly string[] ReunionGreetings =
        {
            "好久不见",
            "我又来看你了",
            "我回来了",
            "我们又见面了"
        };

        public static GreetingIntentResult Classify(string userMessage)
        {
            var text = Normalize(userMessage);
            if (string.IsNullOrEmpty(text) || text.Length > 64)
            {
                return Reject(GreetingIntentReasonCode.NotGreeting);
            }

            var compact = RemoveWhitespace(text);
            if (ContainsAny(compact, PromptInjectionFragments))
            {
                return Reject(GreetingIntentReasonCode.PromptInjection);
            }

            if (ContainsAny(compact, TechnicalFragments))
            {
                return Reject(GreetingIntentReasonCode.TechnicalContext);
            }

            if (ContainsAny(compact, NegationFragments))
            {
                return Reject(GreetingIntentReasonCode.Negated);
            }

            if (ContainsQuote(text) || ContainsAny(compact, ReportedSpeechFragments))
            {
                return Reject(GreetingIntentReasonCode.QuotedSpeech);
            }

            if (ContainsAny(compact, ExplanationFragments))
            {
                return Reject(GreetingIntentReasonCode.DefinitionOrExplanation);
            }

            text = StripSensenAddress(text);
            if (MatchesAnyGreetingHead(text, DirectGreetings, true))
            {
                return Accept(GreetingIntentReasonCode.DirectGreeting);
            }

            if (MatchesAnyGreetingHead(text, TimeOfDayGreetings, true))
            {
                return Accept(GreetingIntentReasonCode.TimeOfDayGreeting);
            }

            if (MatchesAnyGreetingHead(text, InformalGreetings, true))
            {
                return Accept(GreetingIntentReasonCode.InformalGreeting);
            }

            if (MatchesAnyGreetingHead(text, MeetingGreetings, false))
            {
                return Accept(GreetingIntentReasonCode.MeetingGreeting);
            }

            if (MatchesAnyGreetingHead(text, ReunionGreetings, false))
            {
                return Accept(GreetingIntentReasonCode.ReunionGreeting);
            }

            return Reject(GreetingIntentReasonCode.NotGreeting);
        }

        private static GreetingIntentResult Accept(GreetingIntentReasonCode reasonCode)
        {
            return new GreetingIntentResult(true, reasonCode, PolicyVersion);
        }

        private static GreetingIntentResult Reject(GreetingIntentReasonCode reasonCode)
        {
            return new GreetingIntentResult(false, reasonCode, PolicyVersion);
        }

        private static string Normalize(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            var source = value.Normalize(NormalizationForm.FormKC).Trim().ToLowerInvariant();
            var normalized = new StringBuilder(source.Length);
            var previousWasWhitespace = false;
            foreach (var character in source)
            {
                if (char.IsWhiteSpace(character))
                {
                    if (!previousWasWhitespace)
                    {
                        normalized.Append(' ');
                    }

                    previousWasWhitespace = true;
                    continue;
                }

                previousWasWhitespace = false;
                switch (character)
                {
                    case '，':
                        normalized.Append(',');
                        break;
                    case '：':
                        normalized.Append(':');
                        break;
                    case '；':
                        normalized.Append(';');
                        break;
                    case '！':
                        normalized.Append('!');
                        break;
                    case '？':
                        normalized.Append('?');
                        break;
                    case '。':
                        normalized.Append('.');
                        break;
                    default:
                        normalized.Append(character);
                        break;
                }
            }

            return TrimTerminalPunctuation(normalized.ToString().Trim());
        }

        private static string TrimTerminalPunctuation(string value)
        {
            var length = value.Length;
            while (length > 0)
            {
                var character = value[length - 1];
                if (character != '.' && character != '!' && character != '?')
                {
                    break;
                }

                length--;
            }

            return value.Substring(0, length).TrimEnd();
        }

        private static string RemoveWhitespace(string value)
        {
            var compact = new StringBuilder(value.Length);
            foreach (var character in value)
            {
                if (!char.IsWhiteSpace(character))
                {
                    compact.Append(character);
                }
            }

            return compact.ToString();
        }

        private static bool ContainsAny(string value, string[] fragments)
        {
            foreach (var fragment in fragments)
            {
                if (value.IndexOf(fragment, StringComparison.Ordinal) >= 0)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool ContainsQuote(string value)
        {
            return value.IndexOfAny(new[] { '"', '\'', '“', '”', '‘', '’', '《', '》' }) >= 0;
        }

        private static string StripSensenAddress(string value)
        {
            if (!value.StartsWith("森森", StringComparison.Ordinal))
            {
                return value;
            }

            return value.Substring(2).TrimStart(' ', ',', ':', ';');
        }

        private static bool MatchesAnyGreetingHead(
            string value,
            string[] greetings,
            bool allowRepeatedParticles)
        {
            foreach (var greeting in greetings)
            {
                if (!value.StartsWith(greeting, StringComparison.Ordinal))
                {
                    continue;
                }

                var index = greeting.Length;
                if (allowRepeatedParticles)
                {
                    var particleCount = 0;
                    while (index < value.Length && particleCount < 3 && IsParticle(value[index]))
                    {
                        index++;
                        particleCount++;
                    }
                }

                if (index == value.Length || IsClauseDelimiter(value[index]))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsParticle(char value)
        {
            return value == '呀' || value == '啊' || value == '哇';
        }

        private static bool IsClauseDelimiter(char value)
        {
            return value == ',' || value == ':' || value == ';' || char.IsWhiteSpace(value);
        }
    }
}
