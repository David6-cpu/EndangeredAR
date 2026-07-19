using System;
using UnityEngine;

namespace EndangeredAR.Chat
{
    public class LocalKnowledgeChatService : MonoBehaviour
    {
        [SerializeField]
        private KnowledgeEntry[] entries =
        {
            new KnowledgeEntry(
                new[] { "吃", "食物", "food" },
                "我最喜欢森林里的嫩叶啦，也会吃果实和花朵。人类零食闻起来新奇，但真的不适合我。你愿意帮我找一份天然食物吗？",
                new[] { "帮森森寻找食物", "为什么不能投喂？", "你住在哪里？" }
            ),
            new KnowledgeEntry(
                new[] { "住", "栖息", "家", "哪里" },
                "我的家在热带和亚热带森林。树冠连在一起时，我就能轻轻跳过去找食物、找同伴。要是森林被切开，我会有点害怕……",
                new[] { "森林被破坏会怎样？", "我能怎么帮你？", "你吃什么？" }
            ),
            new KnowledgeEntry(
                new[] { "濒危", "危险", "为什么", "原因" },
                "我会变得濒危，主要是因为森林变少、非法捕猎和种群隔离。森林被切碎后，我们很难迁移，也不容易遇到新的伙伴。",
                new[] { "什么是种群隔离？", "怎么保护你？", "你的栖息地在哪里？" }
            ),
            new KnowledgeEntry(
                new[] { "保护", "帮助", "怎么做", "行动" },
                "谢谢你愿意帮我！你可以少浪费纸张、拒绝购买野生动物制品、支持自然保护，也把正确知识告诉朋友。小小行动也会让森林更安全。",
                new[] { "我可以参加什么任务？", "你吃什么？", "为什么要保护森林？" }
            ),
            new KnowledgeEntry(
                new[] { "任务", "游戏", "挑战", "徽章" },
                "我们来玩一个小任务吧：帮我在森林餐桌里找到能吃的天然食物。完成后，我会送你“生态守护者”徽章！",
                new[] { "开始寻找食物", "你喜欢吃什么？", "完成后有什么奖励？" }
            )
        };

        public ChatAnswer Answer(string message)
        {
            var normalized = message == null ? string.Empty : message.Trim();

            foreach (var entry in entries)
            {
                if (entry != null && ContainsAny(normalized, entry.Keywords))
                {
                    return new ChatAnswer(entry.Reply, entry.SuggestedQuestions, true);
                }
            }

            return new ChatAnswer(
                "这个问题我还在努力想呢。你可以先问我吃什么、住在哪里、为什么濒危，或者来帮我完成寻找食物任务，好吗？",
                new[] { "你吃什么？", "帮森森寻找食物", "我怎么保护你？" },
                false
            );
        }

        private static bool ContainsAny(string value, params string[] keywords)
        {
            if (keywords == null)
            {
                return false;
            }

            foreach (var keyword in keywords)
            {
                if (!string.IsNullOrEmpty(keyword) && value.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return true;
                }
            }

            return false;
        }
    }

    [Serializable]
    public class KnowledgeEntry
    {
        public KnowledgeEntry()
        {
        }

        public KnowledgeEntry(string[] keywords, string reply, string[] suggestedQuestions)
        {
            Keywords = keywords;
            Reply = reply;
            SuggestedQuestions = suggestedQuestions;
        }

        public string[] Keywords;
        [TextArea(2, 5)] public string Reply;
        public string[] SuggestedQuestions;
    }

    public struct ChatAnswer
    {
        public ChatAnswer(string reply, string[] suggestedQuestions, bool isMatch)
        {
            Reply = reply;
            SuggestedQuestions = suggestedQuestions;
            IsMatch = isMatch;
        }

        public string Reply { get; }
        public string[] SuggestedQuestions { get; }
        public bool IsMatch { get; }
    }
}
