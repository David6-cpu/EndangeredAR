using System;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace EndangeredAR.Tests.EditMode
{
    public sealed class OnDeviceLLMNativeContractTests
    {
        private static readonly string[] RequiredSymbols =
        {
            "endar_llm_start_load",
            "endar_llm_start_generate",
            "endar_llm_count_tokens",
            "endar_llm_get_state",
            "endar_llm_copy_output",
            "endar_llm_copy_error",
            "endar_llm_copy_metrics_json",
            "endar_llm_cancel",
            "endar_llm_release"
        };

        [Test]
        public void NativeHeader_ExposesOnlyTheLockedCAbi()
        {
            var source = ReadAsset("Plugins/iOS/EndangeredAROnDeviceLLMBridge.h");
            StringAssert.Contains("extern \"C\"", source);
            foreach (var symbol in RequiredSymbols)
            {
                StringAssert.Contains(symbol, source);
            }

            StringAssert.DoesNotContain("std::", source);
            StringAssert.DoesNotContain("UnitySendMessage", source);
            StringAssert.DoesNotContain("callback", source.ToLowerInvariant());
        }

        [Test]
        public void NativeImplementation_UsesSerialWorkerDirectFileLoadAndCancellation()
        {
            var source = ReadAsset("Plugins/iOS/EndangeredAROnDeviceLLMBridge.mm");
            StringAssert.Contains("dispatch_queue_create", source);
            StringAssert.Contains("llama_model_load_from_file", source);
            StringAssert.Contains("llama_model_chat_template", source);
            StringAssert.Contains("llama_chat_apply_template", source);
            StringAssert.Contains("NSJSONSerialization", source);
            StringAssert.Contains("llama_tokenize", source);
            StringAssert.Contains("llama_memory_clear", source);
            StringAssert.Contains("llama_decode", source);
            StringAssert.Contains("llama_sampler_sample", source);
            StringAssert.Contains("llama_sampler_init_penalties", source);
            StringAssert.Contains("llama_supports_gpu_offload", source);
            StringAssert.Contains("cancel_requested", source);
            StringAssert.DoesNotContain("UnitySendMessage", source);
            StringAssert.DoesNotContain("你是森森，请用简短、友好的中文回答。", source);
            StringAssert.DoesNotContain("/Users/", source);
            StringAssert.DoesNotContain("/Applications/", source);
        }

        [Test]
        public void ManagedContract_HasBoundedStatesAndNoUnityObjectsOrCallbacks()
        {
            var stateType = FindType("EndangeredAR.AI.OnDevice.OnDeviceLLMNativeState");
            Assert.That(Enum.GetNames(stateType), Is.EqualTo(new[]
            {
                "Unsupported",
                "Uninitialized",
                "Loading",
                "Ready",
                "Generating",
                "Completed",
                "Cancelled",
                "Error"
            }));

            var interfaceType = FindType("EndangeredAR.AI.OnDevice.IOnDeviceLLMNativeBackend");
            var members = interfaceType.GetMembers(BindingFlags.Public | BindingFlags.Instance);
            Assert.That(members.Any(member => member.Name == "StartLoad"), Is.True);
            Assert.That(members.Any(member => member.Name == "StartGenerate"), Is.True);
            Assert.That(members.Any(member => member.Name == "CountTokens"), Is.True);
            Assert.That(members.Any(member => member.Name == "Cancel"), Is.True);
            Assert.That(members.Any(member => member.Name == "ReadOutput"), Is.True);
            Assert.That(members.Any(member => member.Name == "ReadError"), Is.True);
            Assert.That(members.Any(member => member.Name == "ReadMetrics"), Is.True);
            Assert.That(members.Any(member =>
                member is EventInfo ||
                member is MethodInfo method &&
                method.GetParameters().Any(parameter => typeof(Delegate).IsAssignableFrom(parameter.ParameterType))),
                Is.False);

            foreach (var property in interfaceType.GetProperties())
            {
                Assert.That(typeof(UnityEngine.Object).IsAssignableFrom(property.PropertyType), Is.False);
            }
        }

        [Test]
        public void ManagedBackend_InEditorFailsClosedWithoutCallingNativePlugin()
        {
            var backendType = FindType("EndangeredAR.AI.OnDevice.OnDeviceLLMNativeBackend");
            var backend = Activator.CreateInstance(backendType);
            try
            {
                Assert.That(ReadProperty<bool>(backendType, backend, "IsSupported"), Is.False);
                Assert.That(ReadProperty<object>(backendType, backend, "State").ToString(), Is.EqualTo("Unsupported"));
                Assert.That(Invoke<bool>(backendType, backend, "StartLoad", "model.gguf", 2048, 4, 256, 256), Is.False);
                Assert.That(Invoke<int>(backendType, backend, "CountTokens", "{\"messages\":[]}"), Is.EqualTo(-1));
                Assert.That(Invoke<bool>(backendType, backend, "StartGenerate", "{\"messages\":[]}", 64, 0.7f, 0.8f, 1f, 1u), Is.False);
                Assert.That(Invoke<string>(backendType, backend, "ReadOutput"), Is.Empty);
                Assert.That(Invoke<string>(backendType, backend, "ReadError"), Is.EqualTo("on_device_llm_unsupported"));
            }
            finally
            {
                (backend as IDisposable)?.Dispose();
            }
        }

        private static string ReadAsset(string relativePath)
        {
            var path = Path.Combine(Application.dataPath, relativePath);
            Assert.That(File.Exists(path), Is.True, $"Expected tracked native asset {relativePath}.");
            return File.ReadAllText(path);
        }

        private static Type FindType(string fullName)
        {
            var type = AppDomain.CurrentDomain.GetAssemblies()
                .Select(assembly => assembly.GetType(fullName, false))
                .FirstOrDefault(candidate => candidate != null);
            Assert.That(type, Is.Not.Null, $"Expected {fullName} to exist.");
            return type;
        }

        private static T ReadProperty<T>(Type type, object instance, string name)
        {
            var property = type.GetProperty(name, BindingFlags.Public | BindingFlags.Instance);
            Assert.That(property, Is.Not.Null);
            return (T)property.GetValue(instance);
        }

        private static T Invoke<T>(Type type, object instance, string name, params object[] arguments)
        {
            var method = type.GetMethod(name, BindingFlags.Public | BindingFlags.Instance);
            Assert.That(method, Is.Not.Null);
            return (T)method.Invoke(instance, arguments);
        }
    }
}
