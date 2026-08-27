using System;
using System.Collections;
using System.IO;
using EndangeredAR.AI;
using EndangeredAR.AI.OnDevice;
using NUnit.Framework;
using UnityEngine;

namespace EndangeredAR.Tests.EditMode
{
    public sealed class OnDeviceReleaseCompositionTests
    {
        [Test]
        public void AIConfig_DefaultsToOnDeviceWithoutDevelopmentEndpoint()
        {
            var config = ScriptableObject.CreateInstance<AIConfig>();
            try
            {
                Assert.That(config.providerMode, Is.EqualTo(AIProviderMode.OnDevice));
                Assert.That(config.developmentRemoteServerUrl, Is.Empty);
                Assert.That(config.routeMode, Is.EqualTo(AIRouteMode.LocalOnly));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(config);
            }
        }

        [TestCase(AIProviderMode.DevelopmentRemote)]
        [TestCase(AIProviderMode.DevelopmentCloud)]
        public void ReleaseSelection_AlwaysUsesOnDevice(AIProviderMode configured)
        {
            Assert.That(
                AIProviderSelection.Resolve(configured, developmentRoutesAllowed: false),
                Is.EqualTo(AIProviderMode.OnDevice));
        }

        [TestCase(AIProviderMode.OnDevice)]
        [TestCase(AIProviderMode.DevelopmentRemote)]
        [TestCase(AIProviderMode.DevelopmentCloud)]
        public void DevelopmentSelection_RequiresAndPreservesExplicitMode(AIProviderMode configured)
        {
            Assert.That(
                AIProviderSelection.Resolve(configured, developmentRoutesAllowed: true),
                Is.EqualTo(configured));
        }

        [Test]
        public void BundledModelPath_IsResolvedFromStreamingAssetsWithLockedName()
        {
            var path = OnDeviceLLMProviderFactory.ResolveBundledModelPath("bundle-root");

            Assert.That(path, Is.EqualTo(System.IO.Path.Combine(
                "bundle-root",
                "OnDeviceModels",
                OnDeviceLLMProviderFactory.ModelFileName)));
            Assert.That(path, Does.Not.Contain("Users"));
        }

        [Test]
        public void DevelopmentRemoteProvider_NormalizesLegacyMacTransportProvenance()
        {
            var transport = new LegacyTransport();
            var provider = new DevelopmentRemoteLLMProvider(transport);
            AIResponse response = null;

            var routine = provider.Send(
                new AIRequest { animalId = "sensen", message = "hello" },
                5f,
                value => response = value,
                error => Assert.Fail(error.Code));
            while (routine.MoveNext())
            {
                Assert.That(routine.Current, Is.Null);
            }

            Assert.That(provider.ProviderId, Is.EqualTo("development_remote_llm"));
            Assert.That(response.source, Is.EqualTo("development_remote_llm"));
            Assert.That(response.LanguageGenerator, Is.EqualTo(LanguageGenerator.DevelopmentRemoteLlm));
        }

        [Test]
        public void ProductionBuilder_StagesLockedModelAndNativeFramework()
        {
            var source = File.ReadAllText(Path.GetFullPath("Assets/Editor/EndangeredARIosBuilder.cs"));

            Assert.That(source, Does.Contain("ResolveAndValidateModel"));
            Assert.That(source, Does.Contain("ResolveAndValidateFramework"));
            Assert.That(source, Does.Contain("OnDeviceLLMModelStager.Stage"));
            Assert.That(source, Does.Contain("OnDeviceBuildFlag"));
            Assert.That(source, Does.Contain("BuildOptions.None"));
        }

        [TestCase("Assets/Scripts/AI/LocalLLMProvider.cs")]
        [TestCase("Assets/Scripts/AI/CloudLLMProvider.cs")]
        [TestCase("Assets/Scripts/AI/DevelopmentRemoteLLMProvider.cs")]
        public void DevelopmentProviders_AreExcludedFromReleaseCompilation(string path)
        {
            var source = File.ReadAllText(Path.GetFullPath(path));

            Assert.That(source, Does.StartWith("#if UNITY_EDITOR || DEVELOPMENT_BUILD"));
        }

        private sealed class LegacyTransport : IAIProvider
        {
            public string ProviderId => "local_llm";

            public IEnumerator Send(
                AIRequest request,
                float timeoutSeconds,
                Action<AIResponse> onSuccess,
                Action<AIProviderError> onError)
            {
                var response = new AIResponse
                {
                    animalId = request.animalId,
                    reply = "development reply",
                    source = "local_llm"
                };
                response.LanguageGenerator = LanguageGenerator.LocalLlm;
                response.ContentAuthority = request.ContentAuthority;
                onSuccess(response);
                yield break;
            }
        }
    }
}
