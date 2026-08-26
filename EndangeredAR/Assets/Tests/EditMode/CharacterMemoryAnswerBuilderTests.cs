using EndangeredAR.AI;
using NUnit.Framework;

namespace EndangeredAR.Tests.EditMode
{
    public sealed class CharacterMemoryAnswerBuilderTests
    {
        [Test]
        public void ExplicitRecall_UsesOnlyAvailableDeterministicMilestones()
        {
            var context = AvailableContext();

            var first = CharacterMemoryAnswerBuilder.BuildExplicitRecall(context);
            var second = CharacterMemoryAnswerBuilder.BuildExplicitRecall(context);

            Assert.That(first, Is.EqualTo(second));
            Assert.That(first, Does.Contain("保护森森的森林"));
            Assert.That(first, Does.Contain("森森的食性知识"));
            Assert.That(first, Does.Contain("1枚相关徽章"));
            Assert.That(first, Does.Not.Contain("sensen-food"));
            Assert.That(first, Does.Not.Contain("2026"));
            AssertNoTimeClaim(first);
            Assert.That(CharacterMemoryAnswerBuilder.MemoryRecallAnswerMode, Is.EqualTo("memory_recall"));
        }

        [Test]
        public void ExplicitRecall_EmptyAndUnavailableDoNotFabricateClaims()
        {
            Assert.That(
                CharacterMemoryAnswerBuilder.BuildExplicitRecall(ReadOnlyCharacterMemoryContext.EmptyFor("sensen")),
                Is.EqualTo("我目前没有保存到可用于长期回忆的里程碑记录。"));
            Assert.That(
                CharacterMemoryAnswerBuilder.BuildExplicitRecall(ReadOnlyCharacterMemoryContext.UnavailableFor("sensen")),
                Is.EqualTo("我现在暂时无法读取长期记忆记录。"));
        }

        [Test]
        public void ConversationHistoryBoundary_NeverClaimsStoredConversationTopics()
        {
            var reply = CharacterMemoryAnswerBuilder.BuildConversationHistoryBoundary();

            Assert.That(reply, Does.Contain("不保存完整聊天内容"));
            Assert.That(reply, Does.Contain("最近聊天记录"));
            Assert.That(reply, Does.Contain("长期里程碑记忆"));
            Assert.That(reply, Does.Not.Contain("食性"));
        }

        [Test]
        public void Reunion_UsesAtMostOneMemoryClaimAndSafeTail()
        {
            var reply = CharacterMemoryAnswerBuilder.BuildReunion(
                AvailableContext(),
                "真高兴能继续陪你探索！");

            Assert.That(reply, Does.Contain("保护森森的森林"));
            Assert.That(reply, Does.Not.Contain("森森的食性知识"));
            Assert.That(reply, Does.Not.Contain("徽章"));
            Assert.That(reply, Does.EndWith("真高兴能继续陪你探索！"));
            AssertNoTimeClaim(reply);
        }

        [Test]
        public void Reunion_EmptyAndUnavailableNeverPretendToRemember()
        {
            Assert.That(
                CharacterMemoryAnswerBuilder.BuildReunion(
                    ReadOnlyCharacterMemoryContext.EmptyFor("sensen"),
                    "很高兴又见到你！"),
                Is.EqualTo("很高兴见到你！"));
            Assert.That(
                CharacterMemoryAnswerBuilder.BuildReunion(
                    ReadOnlyCharacterMemoryContext.UnavailableFor("sensen"),
                    "很高兴又见到你！"),
                Is.EqualTo("很高兴见到你！不过我现在暂时无法读取长期记忆记录。"));
        }

        [Test]
        public void ReunionTailGuard_RejectsClaimsTechnicalTextNumbersAndTimes()
        {
            Assert.That(SafeReunionTailGuard.TryAccept("真高兴能继续陪你探索！", out var accepted), Is.True);
            Assert.That(accepted, Is.EqualTo("真高兴能继续陪你探索！"));

            Assert.That(SafeReunionTailGuard.TryAccept("我记得你完成了2项任务。", out _), Is.False);
            Assert.That(SafeReunionTailGuard.TryAccept("昨天我们讨论过食性。", out _), Is.False);
            Assert.That(SafeReunionTailGuard.TryAccept("Animator.SetTrigger(\"Eat\")", out _), Is.False);
            Assert.That(SafeReunionTailGuard.TryAccept("这是一句故意超过安全长度上限的寒暄尾句，它不应该被模型直接拼接到事实前缀之后。", out _), Is.False);
        }

        [Test]
        public void Reunion_UnsafeTailFallsBackToFixedApplicationText()
        {
            var reply = CharacterMemoryAnswerBuilder.BuildReunion(
                AvailableContext(),
                "我记得你完成了99项任务。" );

            Assert.That(reply, Does.EndWith("很高兴又见到你！"));
            Assert.That(reply, Does.Not.Contain("99"));
        }

        [Test]
        public void ExplicitRecall_OmitsWholeClaimsToStayWithinCharacterBudget()
        {
            var longLabel = new string('森', 100);
            var context = ReadOnlyCharacterMemoryContext.Create(
                "sensen",
                CharacterMemoryContextStatus.Available,
                true,
                1,
                1,
                1,
                new[]
                {
                    new ReadOnlyCharacterMemoryMilestone(
                        CharacterMemoryContextMilestoneKind.MissionCompleted,
                        longLabel),
                    new ReadOnlyCharacterMemoryMilestone(
                        CharacterMemoryContextMilestoneKind.KnowledgeLearned,
                        longLabel),
                    new ReadOnlyCharacterMemoryMilestone(
                        CharacterMemoryContextMilestoneKind.AnimalDiscovered,
                        longLabel)
                });

            var reply = CharacterMemoryAnswerBuilder.BuildExplicitRecall(context);

            Assert.That(reply.Length, Is.LessThanOrEqualTo(CharacterMemoryAnswerBuilder.MaximumTextCharacters));
            Assert.That(reply.Contains(new string('森', 99)), Is.EqualTo(reply.Contains(longLabel)));
        }

        private static ReadOnlyCharacterMemoryContext AvailableContext()
        {
            return ReadOnlyCharacterMemoryContext.Create(
                "sensen",
                CharacterMemoryContextStatus.Available,
                true,
                1,
                1,
                1,
                new[]
                {
                    new ReadOnlyCharacterMemoryMilestone(
                        CharacterMemoryContextMilestoneKind.MissionCompleted,
                        "保护森森的森林"),
                    new ReadOnlyCharacterMemoryMilestone(
                        CharacterMemoryContextMilestoneKind.KnowledgeLearned,
                        "森森的食性知识")
                });
        }

        private static void AssertNoTimeClaim(string value)
        {
            Assert.That(value, Does.Not.Contain("刚刚"));
            Assert.That(value, Does.Not.Contain("昨天"));
            Assert.That(value, Does.Not.Contain("上周"));
            Assert.That(value, Does.Not.Contain("上次"));
            Assert.That(value, Does.Not.Contain("最近完成"));
            Assert.That(value, Does.Not.Contain("第一次"));
        }
    }
}
