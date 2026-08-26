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
            var cloud = FakeProvider.Success("cloud_llm", "cloud_llm", "cloud reply");
            var knowledge = FakeProvider.Success("unity_fallback", "unity_fallback", "knowledge reply");
            var router = new AIRouter(null, cloud, knowledge, () => 0f);
            AIResponse response = null;

            Run(router.Route(Request(), AIRouteMode.CloudOnly, 8f, 38f, value => response = value, Fail));

            Assert.That(cloud.CallCount, Is.EqualTo(1));
            Assert.That(knowledge.CallCount, Is.EqualTo(0));
            Assert.That(response.reply, Is.EqualTo("cloud reply"));
            Assert.That(response.source, Is.EqualTo("cloud_llm"));
            Assert.That(response.routeReason, Is.EqualTo("cloud_only"));
            Assert.That(response.ProviderAttempts, Is.EqualTo(new[] { "cloud_llm" }));
            Assert.That(response.FallbackUsed, Is.False);
        }

        [Test]
        public void CloudOnly_WhenCloudFails_UsesKnowledgeFallback()
        {
            var cloud = FakeProvider.Error("cloud_llm");
            var knowledge = FakeProvider.Success("unity_fallback", "unity_fallback", "knowledge reply");
            var router = new AIRouter(null, cloud, knowledge, () => 0f);
            AIResponse response = null;

            Run(router.Route(Request(), AIRouteMode.CloudOnly, 8f, 38f, value => response = value, Fail));

            Assert.That(cloud.CallCount, Is.EqualTo(1));
            Assert.That(knowledge.CallCount, Is.EqualTo(1));
            Assert.That(response.source, Is.EqualTo("unity_fallback"));
            Assert.That(response.routeReason, Is.EqualTo("cloud_only_knowledge_fallback"));
            Assert.That(response.ProviderAttempts, Is.EqualTo(new[] { "cloud_llm", "unity_fallback" }));
            Assert.That(response.FallbackUsed, Is.True);
            Assert.That(response.FallbackReasonCode, Is.EqualTo("provider_failed"));
        }

        [Test]
        public void LocalOnly_UsesLocalAndNeverInvokesCloud()
        {
            var local = FakeProvider.Success("local_llm", "local_llm", "local reply");
            var cloud = FakeProvider.Success("cloud_llm", "cloud_llm", "cloud reply");
            var knowledge = FakeProvider.Success("unity_fallback", "unity_fallback", "knowledge reply");
            var router = new AIRouter(local, cloud, knowledge, () => 0f);
            AIResponse response = null;

            Run(router.Route(Request(), AIRouteMode.LocalOnly, 8f, 38f, value => response = value, Fail));

            Assert.That(local.CallCount, Is.EqualTo(1));
            Assert.That(cloud.CallCount, Is.EqualTo(0));
            Assert.That(knowledge.CallCount, Is.EqualTo(0));
            Assert.That(response.source, Is.EqualTo("local_llm"));
            Assert.That(response.routeReason, Is.EqualTo("local_only"));
            Assert.That(response.ProviderAttempts, Is.EqualTo(new[] { "local_llm" }));
            Assert.That(response.FallbackUsed, Is.False);
        }

        [Test]
        public void LocalOnly_WhenLocalFails_UsesKnowledgeWithoutCloud()
        {
            var local = FakeProvider.Error("local_llm");
            var cloud = FakeProvider.Success("cloud_llm", "cloud_llm", "cloud reply");
            var knowledge = FakeProvider.Success("unity_fallback", "unity_fallback", "knowledge reply");
            var router = new AIRouter(local, cloud, knowledge, () => 0f);
            AIResponse response = null;

            Run(router.Route(Request(), AIRouteMode.LocalOnly, 8f, 38f, value => response = value, Fail));

            Assert.That(local.CallCount, Is.EqualTo(1));
            Assert.That(cloud.CallCount, Is.EqualTo(0));
            Assert.That(knowledge.CallCount, Is.EqualTo(1));
            Assert.That(response.source, Is.EqualTo("unity_fallback"));
            Assert.That(response.routeReason, Is.EqualTo("local_only_knowledge_fallback"));
            Assert.That(response.ProviderAttempts, Is.EqualTo(new[] { "local_llm", "unity_fallback" }));
            Assert.That(response.FallbackUsed, Is.True);
        }

        [Test]
        public void LocalFirst_WhenLocalSucceeds_UsesLocalResponseSourceAndReason()
        {
            var local = FakeProvider.Success("local_llm", "local_llm", "local reply");
            var cloud = FakeProvider.Success("cloud_llm", "cloud_llm", "cloud reply");
            var knowledge = FakeProvider.Success("unity_fallback", "unity_fallback", "knowledge reply");
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
            var local = FakeProvider.Error("local_llm");
            var cloud = FakeProvider.Success("cloud_llm", "cloud_llm", "cloud reply");
            var knowledge = FakeProvider.Success("unity_fallback", "unity_fallback", "knowledge reply");
            var router = new AIRouter(local, cloud, knowledge, () => 0f);
            AIResponse response = null;

            Run(router.Route(Request(), AIRouteMode.LocalFirstCloudFallback, 8f, 38f, value => response = value, Fail));

            Assert.That(local.CallCount, Is.EqualTo(1));
            Assert.That(cloud.CallCount, Is.EqualTo(1));
            Assert.That(knowledge.CallCount, Is.EqualTo(0));
            Assert.That(response.source, Is.EqualTo("cloud_llm"));
            Assert.That(response.routeReason, Is.EqualTo("local_first_cloud_fallback"));
        }

        [Test]
        public void LocalFirst_WhenHttpProvidersFail_UsesKnowledgeFallback()
        {
            var local = FakeProvider.Error("local_llm");
            var cloud = FakeProvider.Error("cloud_llm");
            var knowledge = FakeProvider.Success("unity_fallback", "unity_fallback", "knowledge reply");
            var router = new AIRouter(local, cloud, knowledge, () => 0f);
            AIResponse response = null;

            Run(router.Route(Request(), AIRouteMode.LocalFirstCloudFallback, 8f, 38f, value => response = value, Fail));

            Assert.That(local.CallCount, Is.EqualTo(1));
            Assert.That(cloud.CallCount, Is.EqualTo(1));
            Assert.That(knowledge.CallCount, Is.EqualTo(1));
            Assert.That(response.source, Is.EqualTo("unity_fallback"));
            Assert.That(response.routeReason, Is.EqualTo("local_first_knowledge_fallback"));
        }

        [Test]
        public void LocalCloudAndKnowledgeReceiveTheSameReadOnlyContextSnapshot()
        {
            var local = FakeProvider.Error("local_llm");
            var cloud = FakeProvider.Error("cloud_llm");
            var knowledge = FakeProvider.Success("unity_fallback", "unity_fallback", "knowledge reply");
            var router = new AIRouter(local, cloud, knowledge, () => 0f);
            var request = Request();
            request.Context = ReadOnlyCharacterContext.Create(
                new ReadOnlyCharacterState("sensen", true, 1, 1),
                new ReadOnlyTaskState("food-mission", "帮森森寻找食物", true),
                ReadOnlyInteractionState.Empty);

            Run(router.Route(request, AIRouteMode.LocalFirstCloudFallback, 8f, 38f, Ignore, Fail));

            Assert.That(local.LastRequest, Is.SameAs(request));
            Assert.That(cloud.LastRequest, Is.SameAs(request));
            Assert.That(knowledge.LastRequest, Is.SameAs(request));
            Assert.That(local.LastRequest.Context, Is.SameAs(request.Context));
        }

        [Test]
        public void LocalFirst_GivesCloudOnlyRemainingTotalBudget()
        {
            var now = 0f;
            var local = FakeProvider.Error("local_llm", () => now = 3f);
            var cloud = FakeProvider.Success("cloud_llm", "cloud_llm", "cloud reply");
            var knowledge = FakeProvider.Success("unity_fallback", "unity_fallback", "knowledge reply");
            var router = new AIRouter(local, cloud, knowledge, () => now);

            Run(router.Route(Request(), AIRouteMode.LocalFirstCloudFallback, 8f, 10f, Ignore, Fail));

            Assert.That(local.LastTimeoutSeconds, Is.EqualTo(8f));
            Assert.That(cloud.LastTimeoutSeconds, Is.EqualTo(7f));
        }

        [Test]
        public void LocalFirst_WhenBudgetIsExhausted_UsesKnowledgeWithoutCloud()
        {
            var now = 0f;
            var local = FakeProvider.Error("local_llm", () => now = 10f);
            var cloud = FakeProvider.Success("cloud_llm", "cloud_llm", "cloud reply");
            var knowledge = FakeProvider.Success("unity_fallback", "unity_fallback", "knowledge reply");
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
            var cloud = FakeProvider.SuccessThenError("cloud_llm", "cloud_llm", "cloud reply");
            var knowledge = FakeProvider.Success("unity_fallback", "unity_fallback", "knowledge reply");
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

        [Test]
        public void LocalOnly_ClampsLocalTimeoutToTotalBudget()
        {
            var local = FakeProvider.Success("local_llm", "local_llm", "local reply");
            var knowledge = FakeProvider.Success("unity_fallback", "unity_fallback", "knowledge reply");
            var router = new AIRouter(local, null, knowledge, () => 0f);

            Run(router.Route(Request(), AIRouteMode.LocalOnly, 8f, 3f, Ignore, Fail));

            Assert.That(local.LastTimeoutSeconds, Is.EqualTo(3f));
        }

        [Test]
        public void CloudOnly_WhenProviderRunsPastDeadline_DisposesAndUsesKnowledge()
        {
            var now = 0f;
            var cloud = new DeadlineIgnoringProvider(seconds => now += seconds);
            var knowledge = FakeProvider.Success("unity_fallback", "unity_fallback", "knowledge reply");
            var router = new AIRouter(null, cloud, knowledge, () => now);
            AIResponse response = null;

            Run(router.Route(Request(), AIRouteMode.CloudOnly, 8f, 1f, value => response = value, Fail));

            Assert.That(cloud.Disposed, Is.True);
            Assert.That(knowledge.CallCount, Is.EqualTo(1));
            Assert.That(response.source, Is.EqualTo("unity_fallback"));
            Assert.That(response.routeReason, Is.EqualTo("cloud_only_knowledge_fallback"));
        }

        [Test]
        public void CloudOnly_WhenProviderTimesOutAndKnowledgeFails_ReturnsTimeoutError()
        {
            var now = 0f;
            var cloud = new DeadlineIgnoringProvider(seconds => now += seconds);
            var knowledge = FakeProvider.Error("unity_fallback");
            var router = new AIRouter(null, cloud, knowledge, () => now);
            AIProviderError error = null;

            Run(router.Route(Request(), AIRouteMode.CloudOnly, 8f, 1f, Ignore, value => error = value));

            Assert.That(error, Is.Not.Null);
            Assert.That(error.IsTimeout, Is.True);
        }

        [Test]
        public void CloudOnly_WhenProviderMoveNextThrows_UsesKnowledgeFallback()
        {
            var cloud = new ThrowingProvider();
            var knowledge = FakeProvider.Success("unity_fallback", "unity_fallback", "knowledge reply");
            var router = new AIRouter(null, cloud, knowledge, () => 0f);
            AIResponse response = null;

            Assert.DoesNotThrow(() => Run(router.Route(Request(), AIRouteMode.CloudOnly, 8f, 38f, value => response = value, Fail)));

            Assert.That(knowledge.CallCount, Is.EqualTo(1));
            Assert.That(response.source, Is.EqualTo("unity_fallback"));
        }

        [Test]
        public void CoroutineTimeProviderError_DisposesBeforeLateSuccessAndUsesFallback()
        {
            var cloud = new ErrorThenLateSuccessProvider();
            var knowledge = FakeProvider.Success("unity_fallback", "unity_fallback", "knowledge reply");
            var router = new AIRouter(null, cloud, knowledge, () => 0f);
            var successCount = 0;
            var errorCount = 0;
            AIResponse response = null;

            Run(router.Route(
                Request(),
                AIRouteMode.CloudOnly,
                8f,
                38f,
                value =>
                {
                    successCount++;
                    response = value;
                },
                error => errorCount++));

            Assert.That(cloud.LateSuccessAttempted, Is.False);
            Assert.That(successCount, Is.EqualTo(1));
            Assert.That(errorCount, Is.EqualTo(0));
            Assert.That(response.source, Is.EqualTo("unity_fallback"));
        }

        [Test]
        public void CloudOnly_SuccessCallbackFollowedByInfiniteYields_CompletesAndDisposesImmediately()
        {
            var cloud = CallbackThenInfiniteProvider.Success();
            var knowledge = FakeProvider.Success("unity_fallback", "unity_fallback", "knowledge reply");
            var router = new AIRouter(null, cloud, knowledge, () => 0f);
            AIResponse response = null;

            var completed = RunFor(
                router.Route(Request(), AIRouteMode.CloudOnly, 8f, 38f, value => response = value, Fail),
                12);

            Assert.That(completed, Is.True);
            Assert.That(cloud.Disposed, Is.True);
            Assert.That(knowledge.CallCount, Is.EqualTo(0));
            Assert.That(response.source, Is.EqualTo("cloud_llm"));
        }

        [Test]
        public void CloudOnly_ErrorCallbackFollowedByInfiniteYields_FallsBackAndDisposesImmediately()
        {
            var cloud = CallbackThenInfiniteProvider.Error();
            var knowledge = FakeProvider.Success("unity_fallback", "unity_fallback", "knowledge reply");
            var router = new AIRouter(null, cloud, knowledge, () => 0f);
            AIResponse response = null;

            var completed = RunFor(
                router.Route(Request(), AIRouteMode.CloudOnly, 8f, 38f, value => response = value, Fail),
                12);

            Assert.That(completed, Is.True);
            Assert.That(cloud.Disposed, Is.True);
            Assert.That(knowledge.CallCount, Is.EqualTo(1));
            Assert.That(response.source, Is.EqualTo("unity_fallback"));
        }

        [Test]
        public void CloudOnly_NonNullProviderYield_IsRejectedAndFallsBack()
        {
            var cloud = new NonNullYieldThenSuccessProvider();
            var knowledge = FakeProvider.Success("unity_fallback", "unity_fallback", "knowledge reply");
            var router = new AIRouter(null, cloud, knowledge, () => 0f);
            AIResponse response = null;

            Run(router.Route(Request(), AIRouteMode.CloudOnly, 8f, 38f, value => response = value, Fail));

            Assert.That(cloud.Disposed, Is.True);
            Assert.That(knowledge.CallCount, Is.EqualTo(1));
            Assert.That(response.source, Is.EqualTo("unity_fallback"));
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

        private static bool RunFor(IEnumerator routine, int maximumSteps)
        {
            var routines = new Stack<IEnumerator>();
            routines.Push(routine);
            var steps = 0;

            while (routines.Count > 0 && steps < maximumSteps)
            {
                steps++;
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

            return routines.Count == 0;
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
            public AIRequest LastRequest { get; private set; }

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
                LastRequest = request;
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

        private sealed class DeadlineIgnoringProvider : IAIProvider
        {
            private readonly Action<float> advance;

            public DeadlineIgnoringProvider(Action<float> advance)
            {
                this.advance = advance;
            }

            public string ProviderId => "cloud_llm";
            public bool Disposed { get; private set; }

            public IEnumerator Send(
                AIRequest request,
                float timeoutSeconds,
                Action<AIResponse> onSuccess,
                Action<AIProviderError> onError)
            {
                return new DeadlineIgnoringEnumerator(this, advance, onSuccess);
            }

            private sealed class DeadlineIgnoringEnumerator : IEnumerator, IDisposable
            {
                private readonly DeadlineIgnoringProvider owner;
                private readonly Action<float> advance;
                private readonly Action<AIResponse> onSuccess;
                private int step;

                public DeadlineIgnoringEnumerator(
                    DeadlineIgnoringProvider owner,
                    Action<float> advance,
                    Action<AIResponse> onSuccess)
                {
                    this.owner = owner;
                    this.advance = advance;
                    this.onSuccess = onSuccess;
                }

                public object Current => null;

                public bool MoveNext()
                {
                    if (step == 0)
                    {
                        step++;
                        advance(0.5f);
                        return true;
                    }

                    if (step == 1)
                    {
                        step++;
                        advance(1f);
                        onSuccess?.Invoke(new AIResponse { source = "cloud_llm", reply = "late cloud reply" });
                    }

                    return false;
                }

                public void Reset()
                {
                    throw new NotSupportedException();
                }

                public void Dispose()
                {
                    owner.Disposed = true;
                }
            }
        }

        private sealed class ThrowingProvider : IAIProvider
        {
            public string ProviderId => "cloud_llm";

            public IEnumerator Send(
                AIRequest request,
                float timeoutSeconds,
                Action<AIResponse> onSuccess,
                Action<AIProviderError> onError)
            {
                return new ThrowingEnumerator();
            }

            private sealed class ThrowingEnumerator : IEnumerator
            {
                public object Current => null;

                public bool MoveNext()
                {
                    throw new InvalidOperationException("Test provider failure.");
                }

                public void Reset()
                {
                    throw new NotSupportedException();
                }
            }
        }

        private sealed class ErrorThenLateSuccessProvider : IAIProvider
        {
            public string ProviderId => "cloud_llm";
            public bool LateSuccessAttempted { get; private set; }

            public IEnumerator Send(
                AIRequest request,
                float timeoutSeconds,
                Action<AIResponse> onSuccess,
                Action<AIProviderError> onError)
            {
                return SendAfterError(onSuccess, onError);
            }

            private IEnumerator SendAfterError(Action<AIResponse> onSuccess, Action<AIProviderError> onError)
            {
                onError?.Invoke(new AIProviderError("provider_failed", "Provider failed.", false));
                yield return null;
                LateSuccessAttempted = true;
                onSuccess?.Invoke(new AIResponse { source = "cloud_llm", reply = "late cloud reply" });
            }
        }

        private sealed class CallbackThenInfiniteProvider : IAIProvider
        {
            private readonly bool succeeds;

            private CallbackThenInfiniteProvider(bool succeeds)
            {
                this.succeeds = succeeds;
            }

            public string ProviderId => "cloud_llm";
            public bool Disposed { get; private set; }

            public static CallbackThenInfiniteProvider Success()
            {
                return new CallbackThenInfiniteProvider(true);
            }

            public static CallbackThenInfiniteProvider Error()
            {
                return new CallbackThenInfiniteProvider(false);
            }

            public IEnumerator Send(
                AIRequest request,
                float timeoutSeconds,
                Action<AIResponse> onSuccess,
                Action<AIProviderError> onError)
            {
                return new CallbackThenInfiniteEnumerator(this, succeeds, onSuccess, onError);
            }

            private sealed class CallbackThenInfiniteEnumerator : IEnumerator, IDisposable
            {
                private readonly CallbackThenInfiniteProvider owner;
                private readonly bool succeeds;
                private readonly Action<AIResponse> onSuccess;
                private readonly Action<AIProviderError> onError;
                private bool callbackSent;

                public CallbackThenInfiniteEnumerator(
                    CallbackThenInfiniteProvider owner,
                    bool succeeds,
                    Action<AIResponse> onSuccess,
                    Action<AIProviderError> onError)
                {
                    this.owner = owner;
                    this.succeeds = succeeds;
                    this.onSuccess = onSuccess;
                    this.onError = onError;
                }

                public object Current => null;

                public bool MoveNext()
                {
                    if (!callbackSent)
                    {
                        callbackSent = true;
                        if (succeeds)
                        {
                            onSuccess?.Invoke(new AIResponse { source = "cloud_llm", reply = "cloud reply" });
                        }
                        else
                        {
                            onError?.Invoke(new AIProviderError("provider_failed", "Provider failed.", false));
                        }
                    }

                    return true;
                }

                public void Reset()
                {
                    throw new NotSupportedException();
                }

                public void Dispose()
                {
                    owner.Disposed = true;
                }
            }
        }

        private sealed class NonNullYieldThenSuccessProvider : IAIProvider
        {
            public string ProviderId => "cloud_llm";
            public bool Disposed { get; private set; }

            public IEnumerator Send(
                AIRequest request,
                float timeoutSeconds,
                Action<AIResponse> onSuccess,
                Action<AIProviderError> onError)
            {
                return new NonNullYieldThenSuccessEnumerator(this, onSuccess);
            }

            private sealed class NonNullYieldThenSuccessEnumerator : IEnumerator, IDisposable
            {
                private readonly NonNullYieldThenSuccessProvider owner;
                private readonly Action<AIResponse> onSuccess;
                private readonly object yieldedValue = new object();
                private int step;

                public NonNullYieldThenSuccessEnumerator(NonNullYieldThenSuccessProvider owner, Action<AIResponse> onSuccess)
                {
                    this.owner = owner;
                    this.onSuccess = onSuccess;
                }

                public object Current => yieldedValue;

                public bool MoveNext()
                {
                    if (step == 0)
                    {
                        step++;
                        return true;
                    }

                    if (step == 1)
                    {
                        step++;
                        onSuccess?.Invoke(new AIResponse { source = "cloud_llm", reply = "late cloud reply" });
                    }

                    return false;
                }

                public void Reset()
                {
                    throw new NotSupportedException();
                }

                public void Dispose()
                {
                    owner.Disposed = true;
                }
            }
        }
    }
}
