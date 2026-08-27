using System;
using System.Linq;
using System.Reflection;
using System.Collections;
using EndangeredAR.AI.OnDevice;
using NUnit.Framework;
using UnityEngine.TestTools;

namespace EndangeredAR.Tests.PlayMode
{
    public sealed class OnDeviceLLMSpikeLifecycleTests
    {
        [UnityTest]
        public IEnumerator NativeGenerationPolling_DoesNotBlockUnityFrames()
        {
            var backend = new FakeBackend();
            var type = AppDomain.CurrentDomain.GetAssemblies()
                .Select(assembly => assembly.GetType("EndangeredAR.AI.OnDevice.OnDeviceLLMSpikeRunner", false))
                .FirstOrDefault(candidate => candidate != null);
            Assert.That(type, Is.Not.Null);
            var runner = Activator.CreateInstance(type, backend, 2048, 4, 64);

            Assert.That(Invoke<bool>(runner, "StartLoad", "bundle-model.gguf"), Is.True);
            yield return null;
            Assert.That(backend.StateValue, Is.EqualTo(OnDeviceLLMNativeState.Loading));

            backend.StateValue = OnDeviceLLMNativeState.Ready;
            Invoke<object>(runner, "Poll");
            Assert.That(Invoke<bool>(runner, "StartFixedPrompt"), Is.True);
            yield return null;
            Assert.That(backend.StateValue, Is.EqualTo(OnDeviceLLMNativeState.Generating));

            backend.Output = "设备输出";
            backend.StateValue = OnDeviceLLMNativeState.Completed;
            Invoke<object>(runner, "Poll");
            Assert.That(ReadProperty<string>(runner, "Generator"), Is.EqualTo("on_device_llm"));
            (runner as IDisposable)?.Dispose();
        }

        private static T Invoke<T>(object instance, string methodName, params object[] arguments)
        {
            var method = instance.GetType().GetMethod(methodName, BindingFlags.Public | BindingFlags.Instance);
            Assert.That(method, Is.Not.Null);
            var result = method.Invoke(instance, arguments);
            return result == null ? default : (T)result;
        }

        private static T ReadProperty<T>(object instance, string name)
        {
            var property = instance.GetType().GetProperty(name, BindingFlags.Public | BindingFlags.Instance);
            Assert.That(property, Is.Not.Null);
            return (T)property.GetValue(instance);
        }

        private sealed class FakeBackend : IOnDeviceLLMNativeBackend
        {
            public bool IsSupported => true;
            public OnDeviceLLMNativeState State => StateValue;
            public OnDeviceLLMNativeState StateValue = OnDeviceLLMNativeState.Uninitialized;
            public string Output = string.Empty;

            public bool StartLoad(
                string modelPath,
                int contextSize,
                int threadCount,
                int batchSize,
                int microBatchSize)
            {
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
                StateValue = OnDeviceLLMNativeState.Generating;
                return true;
            }

            public string ReadOutput() => Output;
            public string ReadError() => string.Empty;
            public OnDeviceLLMMetrics ReadMetrics() => OnDeviceLLMMetrics.Empty;
            public void Cancel() { }
            public void Dispose() { }
        }
    }
}
