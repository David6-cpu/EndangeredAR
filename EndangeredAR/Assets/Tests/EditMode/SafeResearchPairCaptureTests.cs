using System.Linq;
using System.Reflection;
using EndangeredAR.AI;
using EndangeredAR.Development;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace EndangeredAR.Tests.EditMode
{
    public sealed class SafeResearchPairCaptureTests
    {
        [SetUp]
        public void SetUp()
        {
            SafeResearchPairCapture.Reset();
        }

        [TearDown]
        public void TearDown()
        {
            SafeResearchPairCapture.Reset();
            var root = GameObject.Find("EndangeredAR Development Tools");
            if (root != null)
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void Capture_AcceptsApprovedSciencePromptWithoutMatchingAssistantText()
        {
            SafeResearchPairCapture.BeginCompletion(17);
            var response = OnDeviceResponse(
                "grounded_fact",
                ContentAuthority.CanonicalKnowledge);

            Assert.That(
                SafeResearchPairCapture.TryRecordAccepted(
                    17,
                    "你的学名是什么？",
                    "我的学名是 Semnopithecus priam。资料来源：GBIF。",
                    response,
                    true),
                Is.True);
            Assert.That(SafeResearchPairCapture.TryCaptureCurrent(out var captured), Is.True);
            Assert.That(captured.PromptId, Is.EqualTo("r34a4_science_name"));
            Assert.That(captured.CompletionId, Is.EqualTo(17));
            Assert.That(captured.FinalSource, Is.EqualTo("on_device_llm"));
            Assert.That(captured.ValidationResult, Is.EqualTo("passed"));
            Assert.That(captured.ContentAuthority, Is.EqualTo("canonical_knowledge"));
        }

        [Test]
        public void Capture_RejectsStaleCompletionAfterANewerRequestBegins()
        {
            SafeResearchPairCapture.BeginCompletion(21);
            SafeResearchPairCapture.BeginCompletion(22);

            Assert.That(
                SafeResearchPairCapture.TryRecordAccepted(
                    21,
                    "你好",
                    "你也好呀！",
                    OnDeviceResponse("social_chat", ContentAuthority.None),
                    true),
                Is.False);
            Assert.That(SafeResearchPairCapture.TryCaptureCurrent(out _), Is.False);
            Assert.That(
                SafeResearchPairCapture.LastFailure,
                Is.EqualTo(SafePairCaptureFailure.StaleCompletion));
        }

        [Test]
        public void Capture_RejectsValidationFailureAndSystemStatus()
        {
            SafeResearchPairCapture.BeginCompletion(30);
            Assert.That(
                SafeResearchPairCapture.TryRecordAccepted(
                    30,
                    "你好",
                    "你也好呀！",
                    OnDeviceResponse("social_chat", ContentAuthority.None),
                    false),
                Is.False);
            Assert.That(
                SafeResearchPairCapture.LastFailure,
                Is.EqualTo(SafePairCaptureFailure.ValidationFailed));

            SafeResearchPairCapture.BeginCompletion(31);
            var status = new AIResponse
            {
                source = "system_status",
                answerMode = "system_status",
                reply = "服务暂时不可用。"
            };
            status.LanguageGenerator = LanguageGenerator.None;
            status.ContentAuthority = ContentAuthority.None;

            Assert.That(
                SafeResearchPairCapture.TryRecordAccepted(
                    31,
                    "你好",
                    status.reply,
                    status,
                    true),
                Is.False);
            Assert.That(
                SafeResearchPairCapture.LastFailure,
                Is.EqualTo(SafePairCaptureFailure.UntrustedSource));
        }

        [Test]
        public void Capture_RejectsUnapprovedChatAndClearsThePreviousSnapshot()
        {
            SafeResearchPairCapture.BeginCompletion(40);
            Assert.That(
                SafeResearchPairCapture.TryRecordAccepted(
                    40,
                    "你好",
                    "你也好呀！",
                    OnDeviceResponse("social_chat", ContentAuthority.None),
                    true),
                Is.True);

            SafeResearchPairCapture.BeginCompletion(41);
            Assert.That(
                SafeResearchPairCapture.TryRecordAccepted(
                    41,
                    "这是未批准的普通聊天",
                    "这条回复不应进入研究捕获。",
                    OnDeviceResponse("social_chat", ContentAuthority.None),
                    true),
                Is.False);
            Assert.That(SafeResearchPairCapture.TryCaptureCurrent(out _), Is.False);
            Assert.That(
                SafeResearchPairCapture.LastFailure,
                Is.EqualTo(SafePairCaptureFailure.UnapprovedPrompt));
        }

        [Test]
        public void DevelopmentPanel_ProvidesAnExplicitCaptureButton()
        {
            var bootstrap = typeof(DevelopmentToolsBootstrap).GetMethod(
                "EnsureInitialized",
                BindingFlags.Static | BindingFlags.NonPublic);
            Assert.That(bootstrap, Is.Not.Null);
            bootstrap.Invoke(null, null);

            var panel = Object.FindFirstObjectByType<SafeResearchPairCapturePanel>(
                FindObjectsInactive.Include);
            Assert.That(panel, Is.Not.Null);
            var awake = typeof(SafeResearchPairCapturePanel).GetMethod(
                "Awake",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(awake, Is.Not.Null);
            awake.Invoke(panel, null);
            var labels = panel.GetComponentsInChildren<Text>(true).Select(value => value.text);
            Assert.That(labels, Does.Contain("CAPTURE CURRENT SAFE PAIR"));
        }

        private static AIResponse OnDeviceResponse(
            string answerMode,
            ContentAuthority authority)
        {
            var response = new AIResponse
            {
                source = "on_device_llm",
                answerMode = answerMode,
                reply = "validated"
            };
            response.LanguageGenerator = LanguageGenerator.OnDeviceLlm;
            response.ContentAuthority = authority;
            return response;
        }
    }
}
