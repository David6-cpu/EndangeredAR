using System;
using System.IO;
using System.Linq;
using System.Reflection;
using EndangeredAR.AI.OnDevice;
using NUnit.Framework;
using UnityEngine;

namespace EndangeredAR.Tests.EditMode
{
    public sealed class OnDeviceLLMSpikeRunnerTests
    {
        private const string RunnerTypeName = "EndangeredAR.AI.OnDevice.OnDeviceLLMSpikeRunner";

        [Test]
        public void Runner_UsesOnlyTheFixedPromptAndSetsProvenanceAfterNativeCompletion()
        {
            var backend = new FakeBackend();
            var runner = CreateRunner(backend);

            Assert.That(Invoke<bool>(runner, "StartLoad", "bundle-model.gguf"), Is.True);
            Assert.That(backend.LoadPath, Is.EqualTo("bundle-model.gguf"));
            Assert.That(backend.ContextSize, Is.EqualTo(2048));

            backend.StateValue = OnDeviceLLMNativeState.Ready;
            Invoke<object>(runner, "Poll");
            Assert.That(Invoke<bool>(runner, "StartFixedPrompt"), Is.True);
            Assert.That(backend.Prompt, Is.EqualTo("用一句中文介绍你自己。"));
            Assert.That(backend.MaxTokens, Is.EqualTo(64));
            Assert.That(ReadProperty<string>(runner, "Generator"), Is.Empty);

            backend.Output = "我是森森，一位喜欢和你分享动物知识的伙伴。";
            backend.StateValue = OnDeviceLLMNativeState.Completed;
            Invoke<object>(runner, "Poll");

            Assert.That(ReadProperty<string>(runner, "Generator"), Is.EqualTo("on_device_llm"));
            Assert.That(ReadProperty<string>(runner, "Output"), Is.EqualTo(backend.Output));
            Assert.That(ReadProperty<string>(runner, "Error"), Is.Empty);
            (runner as IDisposable)?.Dispose();
        }

        [Test]
        public void Runner_ErrorOrEmptyCompletionNeverClaimsOnDeviceGenerator()
        {
            var backend = new FakeBackend();
            var runner = CreateRunner(backend);
            Invoke<bool>(runner, "StartLoad", "bundle-model.gguf");

            backend.StateValue = OnDeviceLLMNativeState.Error;
            backend.Error = "model_load_failed";
            Invoke<object>(runner, "Poll");
            Assert.That(ReadProperty<string>(runner, "Generator"), Is.Empty);
            Assert.That(ReadProperty<string>(runner, "Error"), Is.EqualTo("model_load_failed"));

            (runner as IDisposable)?.Dispose();
        }

        [Test]
        public void Runner_BackgroundCancelsActiveNativeWorkAndCanPollAgain()
        {
            var backend = new FakeBackend { StateValue = OnDeviceLLMNativeState.Generating };
            var runner = CreateRunner(backend);

            Invoke<object>(runner, "OnApplicationPause", true);
            Assert.That(backend.CancelCount, Is.EqualTo(1));

            backend.StateValue = OnDeviceLLMNativeState.Cancelled;
            Invoke<object>(runner, "Poll");
            Assert.That(ReadProperty<string>(runner, "Generator"), Is.Empty);
            Assert.That(ReadProperty<string>(runner, "Status"), Is.EqualTo("cancelled"));
            (runner as IDisposable)?.Dispose();
        }

        [Test]
        public void DevelopmentPanelAndBuilderRemainIsolatedFromProductionChat()
        {
            var panel = ReadAsset("Scripts/Development/OnDeviceLLMSpikePanel.cs");
            StringAssert.Contains("UNITY_EDITOR || DEVELOPMENT_BUILD", panel);
            StringAssert.Contains("StartFixedPrompt", panel);
            StringAssert.DoesNotContain("AIManager", panel);
            StringAssert.DoesNotContain("LocalLLMProvider", panel);
            StringAssert.DoesNotContain("CloudLLMProvider", panel);

            var builder = ReadAsset("Editor/OnDeviceLLM/OnDeviceLLMSpikeIosBuilder.cs");
            StringAssert.Contains("BuildPipeline.BuildPlayer", builder);
            StringAssert.Contains("BuildOptions.Development", builder);
            StringAssert.Contains("targetOSVersionString = MinimumIosVersion", builder);
            StringAssert.Contains("OnDeviceLLMModelStager.Stage", builder);
            StringAssert.Contains("finally", builder);
            StringAssert.Contains("SpikeBuildFlag", builder);
            StringAssert.DoesNotContain("AIManager", builder);
        }

        private static object CreateRunner(IOnDeviceLLMNativeBackend backend)
        {
            var type = FindType(RunnerTypeName);
            var runner = Activator.CreateInstance(type, backend, 2048, 4, 64);
            Assert.That(runner, Is.Not.Null);
            return runner;
        }

        private static Type FindType(string fullName)
        {
            var type = AppDomain.CurrentDomain.GetAssemblies()
                .Select(assembly => assembly.GetType(fullName, false))
                .FirstOrDefault(candidate => candidate != null);
            Assert.That(type, Is.Not.Null, $"Expected {fullName} to exist.");
            return type;
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

        private static string ReadAsset(string relativePath)
        {
            var path = Path.Combine(Application.dataPath, relativePath);
            Assert.That(File.Exists(path), Is.True, $"Expected tracked asset {relativePath}.");
            return File.ReadAllText(path);
        }

        private sealed class FakeBackend : IOnDeviceLLMNativeBackend
        {
            public bool IsSupported => true;
            public OnDeviceLLMNativeState State => StateValue;
            public OnDeviceLLMNativeState StateValue = OnDeviceLLMNativeState.Uninitialized;
            public string LoadPath;
            public int ContextSize;
            public string Prompt;
            public int MaxTokens;
            public string Output = string.Empty;
            public string Error = string.Empty;
            public int CancelCount;

            public bool StartLoad(string modelPath, int contextSize, int threadCount)
            {
                LoadPath = modelPath;
                ContextSize = contextSize;
                StateValue = OnDeviceLLMNativeState.Loading;
                return true;
            }

            public bool StartGenerate(string prompt, int maxTokens)
            {
                Prompt = prompt;
                MaxTokens = maxTokens;
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
