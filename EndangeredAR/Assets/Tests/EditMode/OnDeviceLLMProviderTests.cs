using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using EndangeredAR.AI.OnDevice;
using NUnit.Framework;
using UnityEngine;

namespace EndangeredAR.Tests.EditMode
{
    public sealed class OnDeviceLLMProviderTests
    {
        [Test]
        public void Request_CopiesMessagesAndContainsNoBusinessOrUnityReferences()
        {
            var source = new List<OnDeviceChatMessage>
            {
                new OnDeviceChatMessage("system", "安全规则"),
                new OnDeviceChatMessage("user", "你好")
            };
            var request = new OnDeviceLLMRequest(
                "generation_1",
                source,
                64,
                0.7f,
                0.8f,
                1.05f,
                7u);

            source.Clear();

            Assert.That(request.Messages.Count, Is.EqualTo(2));
            Assert.That(request.Messages[0].Role, Is.EqualTo("system"));
            Assert.That(request.Messages[1].Content, Is.EqualTo("你好"));
            Assert.That(typeof(UnityEngine.Object).IsAssignableFrom(request.GetType()), Is.False);

            var forbidden = new[] { "Progress", "Memory", "Knowledge", "Citation", "Action" };
            var memberTypes = request.GetType()
                .GetMembers(BindingFlags.Public | BindingFlags.Instance)
                .Select(MemberTypeName)
                .Where(value => !string.IsNullOrEmpty(value));
            Assert.That(memberTypes.Any(value => forbidden.Any(value.Contains)), Is.False);
        }

        [Test]
        public void Provider_LoadsOnceThenCompletesMultipleIndependentGenerations()
        {
            var backend = new FakeBackend();
            var provider = CreateProvider(backend);

            var first = Start(provider, "generation_1", out var firstResult, out var firstError);
            Assert.That(first.MoveNext(), Is.True);
            Assert.That(backend.LoadCount, Is.EqualTo(1));
            backend.StateValue = OnDeviceLLMNativeState.Ready;
            Assert.That(first.MoveNext(), Is.True);
            Assert.That(backend.StartGenerateCount, Is.EqualTo(1));
            backend.Output = "第一条回复";
            backend.StateValue = OnDeviceLLMNativeState.Completed;
            Assert.That(first.MoveNext(), Is.False);
            Assert.That(firstError(), Is.Null);
            Assert.That(firstResult().Text, Is.EqualTo("第一条回复"));

            var second = Start(provider, "generation_2", out var secondResult, out var secondError);
            Assert.That(second.MoveNext(), Is.True);
            Assert.That(backend.LoadCount, Is.EqualTo(1));
            Assert.That(backend.StartGenerateCount, Is.EqualTo(2));
            backend.Output = "第二条回复";
            backend.StateValue = OnDeviceLLMNativeState.Completed;
            Assert.That(second.MoveNext(), Is.False);
            Assert.That(secondError(), Is.Null);
            Assert.That(secondResult().Text, Is.EqualTo("第二条回复"));

            provider.Dispose();
        }

        [Test]
        public void Provider_RejectsParallelGenerationAndCancelsOnlyMatchingIdentity()
        {
            var backend = new FakeBackend { StateValue = OnDeviceLLMNativeState.Ready };
            var provider = CreateProvider(backend);
            var first = Start(provider, "generation_1", out _, out _);
            Assert.That(first.MoveNext(), Is.True);
            Assert.That(backend.StateValue, Is.EqualTo(OnDeviceLLMNativeState.Generating));

            var second = Start(provider, "generation_2", out _, out var secondError);
            Assert.That(second.MoveNext(), Is.False);
            Assert.That(secondError().Code, Is.EqualTo("on_device_generation_busy"));

            provider.Cancel("different_generation");
            Assert.That(backend.CancelCount, Is.Zero);
            provider.Cancel("generation_1");
            Assert.That(backend.CancelCount, Is.EqualTo(1));

            backend.StateValue = OnDeviceLLMNativeState.Cancelled;
            Assert.That(first.MoveNext(), Is.False);
            provider.Dispose();
        }

        [Test]
        public void Provider_BackgroundCancelsWorkAndNativeErrorsAreSanitized()
        {
            var backend = new FakeBackend { StateValue = OnDeviceLLMNativeState.Ready };
            var provider = CreateProvider(backend);
            var generation = Start(provider, "generation_1", out _, out var error);
            Assert.That(generation.MoveNext(), Is.True);

            provider.OnApplicationPause(true);
            Assert.That(backend.CancelCount, Is.EqualTo(1));
            backend.Error = "pointer 0x1234 at a local model path";
            backend.StateValue = OnDeviceLLMNativeState.Error;
            Assert.That(generation.MoveNext(), Is.False);

            Assert.That(error().Code, Is.EqualTo("on_device_native_error"));
            StringAssert.DoesNotContain("pointer", error().Message);
            StringAssert.DoesNotContain("path", error().Message);
            provider.Dispose();
        }

        [Test]
        public void ProviderContract_IsNarrowAndHasNoCallbacksOrUnityObjects()
        {
            var type = typeof(IOnDeviceLLMProvider);
            Assert.That(type.GetEvents(), Is.Empty);
            Assert.That(type.GetProperties().Any(property =>
                typeof(UnityEngine.Object).IsAssignableFrom(property.PropertyType)), Is.False);
            Assert.That(type.GetMethods().Any(method => method.Name == "Send"), Is.True);
            Assert.That(type.GetMethods().Any(method => method.Name == "Cancel"), Is.True);
            Assert.That(type.GetMethods().Any(method => method.Name == "OnApplicationPause"), Is.True);
        }

        private static OnDeviceLLMProvider CreateProvider(FakeBackend backend)
        {
            return new OnDeviceLLMProvider(
                backend,
                "bundle-model.gguf",
                OnDeviceLLMRuntimeConfig.FirstProductionProfile);
        }

        private static IEnumerator Start(
            OnDeviceLLMProvider provider,
            string generationId,
            out Func<OnDeviceLLMResult> result,
            out Func<OnDeviceLLMError> error)
        {
            OnDeviceLLMResult capturedResult = null;
            OnDeviceLLMError capturedError = null;
            result = () => capturedResult;
            error = () => capturedError;
            return provider.Send(
                new OnDeviceLLMRequest(
                    generationId,
                    new[]
                    {
                        new OnDeviceChatMessage("system", "安全规则"),
                        new OnDeviceChatMessage("user", "你好")
                    },
                    64,
                    0.7f,
                    0.8f,
                    1.05f,
                    7u),
                5f,
                value => capturedResult = value,
                value => capturedError = value);
        }

        private static string MemberTypeName(MemberInfo member)
        {
            if (member is PropertyInfo property)
            {
                return property.PropertyType.FullName ?? string.Empty;
            }

            if (member is FieldInfo field)
            {
                return field.FieldType.FullName ?? string.Empty;
            }

            return string.Empty;
        }

        private sealed class FakeBackend : IOnDeviceLLMNativeBackend
        {
            public bool IsSupported => true;
            public OnDeviceLLMNativeState State => StateValue;
            public OnDeviceLLMNativeState StateValue = OnDeviceLLMNativeState.Uninitialized;
            public string Output = string.Empty;
            public string Error = string.Empty;
            public int LoadCount;
            public int StartGenerateCount;
            public int CancelCount;

            public bool StartLoad(
                string modelPath,
                int contextSize,
                int threadCount,
                int batchSize,
                int microBatchSize)
            {
                LoadCount++;
                StateValue = OnDeviceLLMNativeState.Loading;
                return true;
            }

            public int CountTokens(string messagesJson) => 8;

            public bool StartGenerate(
                string messagesJson,
                int maxTokens,
                float temperature,
                float topP,
                float repeatPenalty,
                uint seed)
            {
                StartGenerateCount++;
                StateValue = OnDeviceLLMNativeState.Generating;
                return true;
            }

            public string ReadOutput() => Output;
            public string ReadError() => Error;
            public OnDeviceLLMMetrics ReadMetrics() => OnDeviceLLMMetrics.Empty;

            public void Cancel()
            {
                CancelCount++;
            }

            public void Dispose()
            {
            }
        }
    }
}
