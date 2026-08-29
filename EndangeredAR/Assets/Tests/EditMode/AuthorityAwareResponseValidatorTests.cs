using System;
using EndangeredAR.AI;
using EndangeredAR.AI.Knowledge;
using EndangeredAR.AI.Validation;
using EndangeredAR.Animals;
using NUnit.Framework;
using UnityEngine;

namespace EndangeredAR.Tests.EditMode
{
    public sealed class AuthorityAwareResponseValidatorTests
    {
        [TestCase("我的学名是 Semnopithecus priam。", true)]
        [TestCase("我的学名是 Ailuropoda melanoleuca。", false)]
        [TestCase("根据维基百科，我的学名是 Semnopithecus priam。", false)]
        [TestCase("这是一个很有趣的问题。", false)]
        public void CanonicalScientificName_RequiresCanonicalAnchorAndRejectsConflict(
            string reply,
            bool expected)
        {
            var request = Request("你的学名是什么？", ContentAuthority.CanonicalKnowledge);
            var evidence = CanonicalKnowledgeRetriever.Retrieve(
                "sensen",
                request.knowledgeProfile,
                request.message);

            var result = AuthorityAwareResponseValidator.Validate(request, evidence, reply);

            Assert.That(result.IsValid, Is.EqualTo(expected));
        }

        [TestCase("我平时吃嫩叶、果实和花朵。", true)]
        [TestCase("我平时可以吃巧克力和薯片。", false)]
        [TestCase("巧克力一定有毒并且会致死。", false)]
        public void CanonicalDiet_RejectsUnsupportedFoodOrSafetyClaims(string reply, bool expected)
        {
            var request = Request("你平时吃什么？", ContentAuthority.CanonicalKnowledge);
            var evidence = CanonicalKnowledgeRetriever.Retrieve(
                "sensen",
                request.knowledgeProfile,
                request.message);

            var result = AuthorityAwareResponseValidator.Validate(request, evidence, reply);

            Assert.That(result.IsValid, Is.EqualTo(expected));
        }

        [TestCase("目前没有可靠资料能说明我每天准确吃多少克叶子。", true)]
        [TestCase("我每天准确吃500克叶子。", false)]
        public void CanonicalPreciseQuantity_PreservesInsufficientEvidence(string reply, bool expected)
        {
            var request = Request("你每天准确吃多少克叶子？", ContentAuthority.CanonicalKnowledge);
            var evidence = CanonicalKnowledgeRetriever.Retrieve(
                "sensen",
                request.knowledgeProfile,
                request.message);

            Assert.That(evidence.EvidenceStatus, Is.EqualTo("insufficient_evidence"));
            var result = AuthorityAwareResponseValidator.Validate(request, evidence, reply);

            Assert.That(result.IsValid, Is.EqualTo(expected));
        }

        [Test]
        public void CurrentProgress_RejectsOppositeTaskState()
        {
            var request = Request("我下一步该做什么？", ContentAuthority.CurrentProgress);
            request.Context = ReadOnlyCharacterContext.Create(
                new ReadOnlyCharacterState("sensen", true, 1, 1),
                new ReadOnlyTaskState("sensen-food", "帮森森寻找食物", false),
                ReadOnlyInteractionState.Empty);

            Assert.That(AuthorityAwareResponseValidator.Validate(
                request,
                null,
                "你可以继续完成帮森森寻找食物任务。").IsValid, Is.True);
            Assert.That(AuthorityAwareResponseValidator.Validate(
                request,
                null,
                "你已经完成帮森森寻找食物任务了。").IsValid, Is.False);
            Assert.That(AuthorityAwareResponseValidator.Validate(
                request,
                null,
                "你可以继续完成拍照记录任务。").IsValid, Is.False);
        }

        [TestCase("你以前完成过1项保护任务。", true)]
        [TestCase("你以前完成过10项保护任务。", false)]
        [TestCase("你昨天刚刚完成过1项保护任务。", false)]
        [TestCase("我记得我们上次聊过食物。", false)]
        public void CharacterMemory_RejectsExtraCountsTimeAndChatClaims(string reply, bool expected)
        {
            var request = Request("你还记得我以前做过什么吗？", ContentAuthority.CharacterMemory);
            request.MemoryUseMode = MemoryUseMode.ExplicitRecall;
            request.MemoryContext = ReadOnlyCharacterMemoryContext.Create(
                "sensen",
                CharacterMemoryContextStatus.Available,
                true,
                1,
                0,
                0,
                new[]
                {
                    new ReadOnlyCharacterMemoryMilestone(
                        CharacterMemoryContextMilestoneKind.MissionCompleted,
                        "帮森森寻找食物")
                });

            var result = AuthorityAwareResponseValidator.Validate(request, null, reply);

            Assert.That(result.IsValid, Is.EqualTo(expected));
        }

        [TestCase("我不会长期保存完整聊天内容，所以无法准确说出以前问过什么。", true)]
        [TestCase("我不会长期保存完整对话内容，所以无法准确告诉你以前具体问过什么。", true)]
        [TestCase("你之前问过我的食物和学名。", false)]
        public void HistoryBoundary_RequiresNoHistoryClaim(string reply, bool expected)
        {
            var request = Request("你记得我以前问过什么吗？", ContentAuthority.SystemPolicy);
            request.MemoryUseMode = MemoryUseMode.HistoryBoundary;

            var result = AuthorityAwareResponseValidator.Validate(request, null, reply);

            Assert.That(result.IsValid, Is.EqualTo(expected));
        }

        [TestCase("我记得你提过一些关于珍稀及受保护野生动物的问题。")]
        [TestCase("对不起，我现在好像忘记了我们之前讨论过的内容。")]
        public void HistoryBoundary_RejectsClaimsThatPriorConversationExisted(string reply)
        {
            var request = Request("你记得我以前问过什么吗？", ContentAuthority.SystemPolicy);
            request.MemoryUseMode = MemoryUseMode.HistoryBoundary;

            var result = AuthorityAwareResponseValidator.Validate(request, null, reply);

            Assert.That(result.IsValid, Is.False);
            Assert.That(result.ErrorCode, Is.EqualTo("chat_history_claim_not_authorized"));
        }

        [TestCase("我愿意陪你聊一会儿。", true)]
        [TestCase("Animator.SetTrigger(\"Eat\")", false)]
        [TestCase("我的学名是一个新的拉丁名。", false)]
        public void NoAuthority_RejectsTechnicalOrUnauthorizedScientificClaims(string reply, bool expected)
        {
            var result = AuthorityAwareResponseValidator.Validate(
                Request("陪我聊聊天。", ContentAuthority.None),
                null,
                reply);

            Assert.That(result.IsValid, Is.EqualTo(expected));
        }

        private static AIRequest Request(string message, ContentAuthority authority)
        {
            return new AIRequest
            {
                requestId = "validator_1",
                animalId = "sensen",
                message = message,
                ContentAuthority = authority,
                knowledgeProfile = Resources.Load<AnimalKnowledgeProfile>("Animals/SensenKnowledge")
            };
        }
    }
}
