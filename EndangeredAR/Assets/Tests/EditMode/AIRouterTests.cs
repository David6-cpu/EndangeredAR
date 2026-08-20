using System;
using System.Collections;
using System.Collections.Generic;
using EndangeredAR.AI;
using NUnit.Framework;

namespace EndangeredAR.Tests.EditMode
{
    public class AIRouterTests
    {
        [Test]
        public void CloudOnly_UsesCloudResponseSourceAndReason()
        {
            var cloud = FakeProvider.Success("cloud", "cloud_proxy", "cloud reply");
            var knowledge = FakeProvider.Success("knowledge", "unity_knowledge", "knowledge reply");
            var router = new AIRouter(null, cloud, knowledge, () => 0f);
            AIResponse response = null;

            Run(router.Route(Request(), AIRouteMode.CloudOnly, 8f, 38f, value => response = value, Fail));

            Assert.That(cloud.CallCount, Is.EqualTo(1));
            Assert.That(knowledge.CallCount, Is.EqualTo(0));
            Assert.That(response.reply, Is.EqualTo("cloud reply"));
            Assert.That(response.source, Is.EqualTo("cloud_proxy"));
            Assert.That(response.routeReason, Is.EqualTo("cloud_only"));
        }

        [Test]
        public void CloudOnly_WhenCloudFails_UsesKnowledgeFallback()
        {
            var cloud = FakeProvider.Error("cloud");
            var knowledge = FakeProvider.Success("knowledge", "unity_knowledge", "knowledge reply");
            var router = new AIRouter(null, cloud, knowledge, () => 0f);
            AIResponse response = null;

            Run(router.Route(Request(), AIRouteMode.CloudOnly, 8f, 38f, value => response = value, Fail));

            Assert.That(cloud.CallCount, Is.EqualTo(1));
            Assert.That(knowledge.CallCount, Is.EqualTo(1));
            Assert.That(response.source, Is.EqualTo("unity_knowledge"));
            Assert.That(response.routeReason, Is.EqualTo("cloud_only_knowledge_fallback"));
        }

        [Test]
        public void LocalOnly_UsesLocalAndNeverInvokesCloud()
        {
            var local = FakeProvider.Success("local", "local_llm", "local reply");
            var cloud = FakeProvider.Success("cloud", "cloud_proxy", "cloud reply");
            var knowledge = FakeProvider.Success("knowledge", "unity_knowledge", "knowledge reply");
            var router = new AIRouter(local, cloud, knowledge, () => 0f);
            AIResponse response = null;

            Run(router.Route(Request(), AIRouteMode.LocalOnly, 8f, 38f, value => response = value, Fail));

            Assert.That(local.CallCount, Is.EqualTo(1));
            Assert.That(cloud.CallCount, Is.EqualTo(0));
            Assert.That(knowledge.CallCount, Is.EqualTo(0));
            Assert.That(response.source, Is.EqualTo("local_llm"));
            Assert.That(response.routeReason, Is.EqualTo("local_only"));
        }

        [Test]
        public void LocalOnly_WhenLocalFails_UsesKnowledgeWithoutCloud()
        {
            var local = FakeProvider.Error("local");
            var cloud = FakeProvider.Success("cloud", "cloud_proxy", "cloud reply");
            var knowledge = FakeProvider.Success("knowledge", "unity_knowledge", "knowledge reply");
            var router = new AIRouter(local, cloud, knowledge, () => 0f);
            AIResponse response = null;

            Run(router.Route(Request(), AIRouteMode.LocalOnly, 8f, 38f, value => response = value, Fail));

            Assert.That(local.CallCount, Is.EqualTo(1));
            Assert.That(cloud.CallCount, Is.EqualTo(0));
            Assert.That(knowledge.CallCount, Is.EqualTo(1));
            Assert.That(response.source, Is.EqualTo("unity_knowledge"));
            Assert.That(response.routeReason, Is.EqualTo("local_only_knowledge_fallback"));
        }

        [Test]
        public void LocalFirst_WhenLocalSucceeds_UsesLocalResponseSourceAndReason()
        {
            var local = FakeProvider.Success("local", "local_llm", "local reply");
            var cloud = FakeProvider.Success("cloud", "cloud_proxy", "cloud reply");
            var knowledge = FakeProvider.Success("knowledge", "unity_knowledge", "knowledge reply");
            var router = new AIRouter(local, cloud, knowledge, () => 0f);
            AIResponse response = null;

            Run(router.Route(Request(), AIRouteMode.LocalFirstCloudFallback, 8f, 38f, value => response = value, Fail));

            Assert.That(local.CallCount, Is.EqualTo(1));
            Assert.That(cloud.CallCount, Is.EqualTo(0));
            Assert.That(knowledge.CallCount, Is.EqualTo(0));
            Assert.That(response.source, Is.EqualTo("local_llm"));
            Assert.That(response.routeReason, Is.EqualTo("local_first"));
        }

        [Test]
        public void LocalFirst_WhenLocalFails_UsesCloudFallback()
        {
            var local = FakeProvider.Error("local");
            var cloud = FakeProvider.Success("cloud", "cloud_proxy", "cloud reply");
            var knowledge = FakeProvider.Success("knowledge", "unity_knowledge", "knowledge reply");
            var router = new AIRouter(local, cloud, knowledge, () => 0f);
            AIResponse response = null;

            Run(router.Route(Request(), AIRouteMode.LocalFirstCloudFallback, 8f, 38f, value => response = value, Fail));

            Assert.That(local.CallCount, Is.EqualTo(1));
            Assert.That(cloud.CallCount, Is.EqualTo(1));
            Assert.That(knowledge.CallCount, Is.EqualTo(0));
            Assert.That(response.source, Is.EqualTo("cloud_proxy"));
            Assert.That(response.routeReason, Is.EqualTo("local_first_cloud_fallback"));
        }

        [Test]
        public void LocalFirst_WhenHttpProvidersFail_UsesKnowledgeFallback()
        {
            var local = FakeProvider.Error("local");
            var cloud = FakeProvider.Error("cloud");
            var knowledge = FakeProvider.Success("knowledge", "unity_knowledge", "knowledge reply");
            var router = new AIRouter(local, cloud, knowledge, () => 0f);
            AIResponse response = null;

            Run(router.Route(Request(), AIRouteMode.LocalFirstCloudFallback, 8f, 38f, value => response = value, Fail));

            Assert.That(local.CallCount, Is.EqualTo(1));
            Assert.That(cloud.CallCount, Is.EqualTo(1));
            Assert.That(knowledge.CallCount, Is.EqualTo(1));
            Assert.That(response.source, Is.EqualTo("unity_knowledge"));
            Assert.That(response.routeReason, Is.EqualTo("local_first_knowledge_fallback"));
        }

        [Test]
        public void LocalFirst_GivesCloudOnlyRemainingTotalBudget()
        {
            var now = 0f;
            var local = FakeProvider.Error("local", () => now = 3f);
            var cloud = FakeProvider.Success("cloud", "cloud_proxy", "cloud reply");
            var knowledge = FakeProvider.Success("knowledge", "unity_knowledge", "knowledge reply");
            var router = new AIRouter(local, cloud, knowledge, () => now);

            Run(router.Route(Request(), AIRouteMode.LocalFirstCloudFallback, 8f, 10f, Ignore, Fail));

            Assert.That(local.LastTimeoutSeconds, Is.EqualTo(8f));
            Assert.That(cloud.LastTimeoutSeconds, Is.EqualTo(7f));
        }

        [Test]
        public void LocalFirst_WhenBudgetIsExhausted_UsesKnowledgeWithoutCloud()
        {
            var now = 0f;
            var local = FakeProvider.Error("local", () => now = 10f);
            var cloud = FakeProvider.Success("cloud", "cloud_proxy", "cloud reply");
            var knowledge = FakeProvider.Success("knowledge", "unity_knowledge", "knowledge reply");
            var router = new AIRouter(local, cloud, knowledge, () => now);
            AIResponse response = null;

            Run(router.Route(Request(), AIRouteMode.LocalFirstCloudFallback, 8f, 10f, value => response = value, Fail));

            Assert.That(cloud.CallCount, Is.EqualTo(0));
            Assert.That(knowledge.CallCount, Is.EqualTo(1));
            Assert.That(response.routeReason, Is.EqualTo("local_first_knowledge_fallback"));
        }

        [Test]
        public void SynchronousProviderCallbacks_CompleteRouteOnlyOnce()
        {
            var cloud = FakeProvider.SuccessThenError("cloud", "cloud_proxy", "cloud reply");
            var knowledge = FakeProvider.Success("knowledge", "unity_knowledge", "knowledge reply");
            var router = new AIRouter(null, cloud, knowledge, () => 0f);
            var successCount = 0;
            var errorCount = 0;

            Run(router.Route(
                Request(),
                AIRouteMode.CloudOnly,
                8f,
                38f,
                value => successCount++,
                error => errorCount++));

            Assert.That(successCount, Is.EqualTo(1));
            Assert.That(errorCount, Is.EqualTo(0));
            Assert.That(knowledge.CallCount, Is.EqualTo(0));
        }

        private static AIRequest Request()
        {
            return new AIRequest
            {
                requestId = "request-1",
                animalId = "sensen",
                message = "How are you?"
            };
        }

        private static void Run(IEnumerator routine)
        {
            var routines = new Stack<IEnumerator>();
            routines.Push(routine);

            while (routines.Count > 0)
            {
                var current = routines.Peek();
                if (!current.MoveNext())
                {
                    routines.Pop();
                    continue;
                }

                var nestedRoutine = current.Current as IEnumerator;
                if (nestedRoutine != null)
                {
                    routines.Push(nestedRoutine);
                }
            }
        }

        private static void Ignore(AIResponse response)
        {
        }

        private static void Fail(AIProviderError error)
        {
            Assert.Fail(error == null ? "Route failed without an error." : error.Code + ": " + error.Message);
        }

        private sealed class FakeProvider : IAIProvider
        {
            private readonly AIResponse response;
            private readonly AIProviderError error;
            private readonly Action beforeCallback;
            private readonly bool callbackSynchronously;

            private FakeProvider(
                string providerId,
                AIResponse response,
                AIProviderError error,
                Action beforeCallback,
                bool callbackSynchronously)
            {
                ProviderId = providerId;
                this.response = response;
                this.error = error;
                this.beforeCallback = beforeCallback;
                this.callbackSynchronously = callbackSynchronously;
            }

            public string ProviderId { get; }
            public int CallCount { get; private set; }
            public float LastTimeoutSeconds { get; private set; }

            public static FakeProvider Success(string providerId, string source, string reply)
            {
                return new FakeProvider(providerId, new AIResponse { source = source, reply = reply }, null, null, false);
            }

            public static FakeProvider Error(string providerId, Action beforeCallback = null)
            {
                return new FakeProvider(providerId, null, new AIProviderError("provider_failed", "Provider failed.", false), beforeCallback, false);
            }

            public static FakeProvider SuccessThenError(string providerId, string source, string reply)
            {
                return new FakeProvider(
                    providerId,
                    new AIResponse { source = source, reply = reply },
                    new AIProviderError("provider_failed", "Provider failed.", false),
                    null,
                    true);
            }

            public IEnumerator Send(
                AIRequest request,
                float timeoutSeconds,
                Action<AIResponse> onSuccess,
                Action<AIProviderError> onError)
            {
                CallCount++;
                LastTimeoutSeconds = timeoutSeconds;
                if (callbackSynchronously)
                {
                    Complete(onSuccess, onError);
                    return Empty();
                }

                return CompleteDuringEnumeration(onSuccess, onError);
            }

            private IEnumerator CompleteDuringEnumeration(Action<AIResponse> onSuccess, Action<AIProviderError> onError)
            {
                Complete(onSuccess, onError);
                yield break;
            }

            private static IEnumerator Empty()
            {
                yield break;
            }

            private void Complete(Action<AIResponse> onSuccess, Action<AIProviderError> onError)
            {
                beforeCallback?.Invoke();
                if (response != null)
                {
                    onSuccess?.Invoke(response);
                }

                if (error != null)
                {
                    onError?.Invoke(error);
                }
            }
        }
    }
}
