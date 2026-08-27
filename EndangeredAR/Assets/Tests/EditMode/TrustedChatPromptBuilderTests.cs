using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using EndangeredAR.AI;
using EndangeredAR.AI.OnDevice;
using EndangeredAR.AI.Prompt;
using NUnit.Framework;
using UnityEngine;

namespace EndangeredAR.Tests.EditMode
{
    public sealed class TrustedChatPromptBuilderTests
    {
        [TestCase(ContentAuthority.None, false, false, false, false)]
        [TestCase(ContentAuthority.CanonicalKnowledge, false, false, true, false)]
        [TestCase(ContentAuthority.CurrentProgress, true, false, false, false)]
        [TestCase(ContentAuthority.CharacterMemory, false, true, false, false)]
        [TestCase(ContentAuthority.SystemPolicy, false, false, false, true)]
        public void Builder_IncludesOnlyTheSelectedAuthoritySection(
            ContentAuthority authority,
            bool hasCurrent,
            bool hasMemory,
            bool hasEvidence,
            bool hasPolicy)
        {
            var prompt = TrustedChatPromptBuilder.Build(
                CreateInput(authority, Array.Empty<OnDeviceChatMessage>()),
                new OnDevicePromptBudget(256, 32, 16),
                new FixedTokenCounter(40));
            var system = prompt.Messages.Single(message => message.Role == "system").Content;

            Assert.That(system.Contains("CURRENT READ-ONLY STATE"), Is.EqualTo(hasCurrent));
            Assert.That(system.Contains("PAST CHARACTER MEMORY"), Is.EqualTo(hasMemory));
            Assert.That(system.Contains("CANONICAL EVIDENCE"), Is.EqualTo(hasEvidence));
            Assert.That(system.Contains("SYSTEM POLICY"), Is.EqualTo(hasPolicy));
            Assert.That(prompt.Messages.Last().Role, Is.EqualTo("user"));
            Assert.That(prompt.Messages.Last().Content, Is.EqualTo("当前问题"));
        }

        [Test]
        public void Builder_DropsOldestCompleteHistoryTurnFirst()
        {
            var history = new[]
            {
                new OnDeviceChatMessage("user", "old-user"),
                new OnDeviceChatMessage("assistant", "old-assistant"),
                new OnDeviceChatMessage("user", "new-user"),
                new OnDeviceChatMessage("assistant", "new-assistant")
            };

            var prompt = TrustedChatPromptBuilder.Build(
                CreateInput(ContentAuthority.None, history),
                new OnDevicePromptBudget(256, 100, 100),
                new PerMessageTokenCounter(10));

            Assert.That(prompt.DroppedHistoryMessages, Is.EqualTo(2));
            Assert.That(prompt.Messages.Select(message => message.Content), Does.Not.Contain("old-user"));
            Assert.That(prompt.Messages.Select(message => message.Content), Does.Not.Contain("old-assistant"));
            Assert.That(prompt.Messages.Select(message => message.Content), Does.Contain("new-user"));
            Assert.That(prompt.Messages.Select(message => message.Content), Does.Contain("new-assistant"));
        }

        [Test]
        public void Builder_NeverPartiallyTruncatesAuthorityOrCurrentUserMessage()
        {
            var prompt = TrustedChatPromptBuilder.Build(
                CreateInput(ContentAuthority.CanonicalKnowledge, Array.Empty<OnDeviceChatMessage>()),
                new OnDevicePromptBudget(256, 32, 16),
                new FixedTokenCounter(80));
            var system = prompt.Messages[0].Content;

            StringAssert.Contains("可信事实整句，不得截断。", system);
            Assert.That(prompt.Messages.Last().Content, Is.EqualTo("当前问题"));
            Assert.That(prompt.PromptTokens, Is.EqualTo(80));
        }

        [Test]
        public void Builder_FailsClosedWhenMinimumAuthoritativePromptDoesNotFit()
        {
            Assert.Throws<OnDevicePromptBudgetExceededException>(() =>
                TrustedChatPromptBuilder.Build(
                    CreateInput(ContentAuthority.CanonicalKnowledge, Array.Empty<OnDeviceChatMessage>()),
                    new OnDevicePromptBudget(256, 100, 80),
                    new FixedTokenCounter(100)));
        }

        [Test]
        public void CSharpPromptCode_DoesNotHardcodeQwenSpecialTokens()
        {
            var directory = Path.Combine(Application.dataPath, "Scripts", "AI");
            var source = string.Join("\n", Directory.GetFiles(directory, "*.cs", SearchOption.AllDirectories)
                .Select(File.ReadAllText));
            StringAssert.DoesNotContain("<|im_start|>", source);
            StringAssert.DoesNotContain("<|im_end|>", source);
        }

        private static TrustedChatPromptInput CreateInput(
            ContentAuthority authority,
            IReadOnlyList<OnDeviceChatMessage> history)
        {
            return new TrustedChatPromptInput(
                "你是森森，只能依据应用提供的权限回答。",
                "当前问题",
                history,
                authority,
                "当前任务尚未完成。",
                "用户以前完成过一项任务。",
                "可信事实整句，不得截断。",
                "长期角色记忆不保存完整聊天内容。");
        }

        private sealed class FixedTokenCounter : IOnDeviceTokenCounter
        {
            private readonly int value;

            public FixedTokenCounter(int value)
            {
                this.value = value;
            }

            public int CountTokens(IReadOnlyList<OnDeviceChatMessage> messages) => value;
        }

        private sealed class PerMessageTokenCounter : IOnDeviceTokenCounter
        {
            private readonly int perMessage;

            public PerMessageTokenCounter(int perMessage)
            {
                this.perMessage = perMessage;
            }

            public int CountTokens(IReadOnlyList<OnDeviceChatMessage> messages) => messages.Count * perMessage;
        }
    }
}
