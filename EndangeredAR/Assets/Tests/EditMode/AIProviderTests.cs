using System;
using System.Collections;
using System.Collections.Generic;
using EndangeredAR.AI;
using EndangeredAR.API;
using EndangeredAR.Chat;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace EndangeredAR.Tests.EditMode
{
    public class AIProviderTests
    {
        private readonly List<UnityEngine.Object> createdObjects = new List<UnityEngine.Object>();

        [TearDown]
        public void TearDown()
        {
            foreach (var createdObject in createdObjects)
            {
                UnityEngine.Object.DestroyImmediate(createdObject);
            }

            createdObjects.Clear();
        }

        [Test]
        public void AIConfig_DefaultsToOnDeviceOnlyWithExistingBudgets()
        {
            var config = ScriptableObject.CreateInstance<AIConfig>();
            createdObjects.Add(config);

            Assert.That(config.routeMode, Is.EqualTo(AIRouteMode.LocalOnly));
            Assert.That(config.providerMode, Is.EqualTo(AIProviderMode.OnDevice));
            Assert.That(config.developmentRemoteServerUrl, Is.Empty);
            Assert.That(config.localTimeoutSeconds, Is.EqualTo(8f));
            Assert.That(config.totalTimeoutSeconds, Is.EqualTo(38f));
        }

        [Test]
        public void ChatApiClient_ProvidesTimeoutOverloadAndRoundsUpSubsecondBudgets()
        {
            var overload = typeof(ChatApiClient).GetMethod(
                "SendMessage",
                new[]
                {
                    typeof(string), typeof(string), typeof(ChatMessage[]), typeof(float),
                    typeof(Action<ChatResponse>), typeof(Action<string>)
                });

            Assert.That(overload, Is.Not.Null);
            Assert.That(ChatApiClient.ToUnityTimeoutSeconds(0.1f), Is.EqualTo(1));
            Assert.That(ChatApiClient.ToUnityTimeoutSeconds(8f), Is.EqualTo(8));
            Assert.That(ChatApiClient.ToUnityTimeoutSeconds(8.1f), Is.EqualTo(9));
        }

        [Test]
        public void DevelopmentPlayers_AllowHttpOnlyForLocalDeviceValidation()
        {
            Assert.That(
                PlayerSettings.insecureHttpOption,
                Is.EqualTo(InsecureHttpOption.DevelopmentOnly));
        }

        [Test]
        public void ChatApiClient_LegacyOverloadsForwardThirtyFiveSecondTimeout()
        {
            var client = CreateControlledChatClient();

            RunCoroutine(client.SendMessage("sensen", "First", IgnoreChatResponse, FailChat));
            Assert.That(client.LastTimeoutSeconds, Is.EqualTo(35f));
            Assert.That(client.LastHistory, Is.Empty);

            var history = new[] { new ChatMessage { role = "user", content = "Earlier" } };
            RunCoroutine(client.SendMessage("sensen", "Second", history, IgnoreChatResponse, FailChat));
            Assert.That(client.LastTimeoutSeconds, Is.EqualTo(35f));
            Assert.That(client.LastHistory, Is.SameAs(history));
        }

        [TestCase(UnityEngine.Networking.UnityWebRequest.Result.ConnectionError, 0L, "Request timeout", "cloud_timeout")]
        [TestCase(UnityEngine.Networking.UnityWebRequest.Result.ConnectionError, 0L, "Cannot resolve destination host", "cloud_dns_failed")]
        [TestCase(UnityEngine.Networking.UnityWebRequest.Result.ConnectionError, 0L, "Local network access denied", "cloud_local_network_denied")]
        [TestCase(UnityEngine.Networking.UnityWebRequest.Result.ConnectionError, 0L, "Insecure connection not allowed", "cloud_transport_security_blocked")]
        [TestCase(UnityEngine.Networking.UnityWebRequest.Result.ConnectionError, 0L, "Cannot connect to destination host", "cloud_connection_failed")]
        [TestCase(UnityEngine.Networking.UnityWebRequest.Result.ProtocolError, 503L, "HTTP/1.1 503", "cloud_http_503")]
        [TestCase(UnityEngine.Networking.UnityWebRequest.Result.DataProcessingError, 200L, "Malformed data", "cloud_data_processing_error")]
        public void ChatApiClient_ClassifiesTransportFailuresWithoutExposingRequestData(
            UnityEngine.Networking.UnityWebRequest.Result result,
            long responseCode,
            string error,
            string expected)
        {
            Assert.That(ChatApiClient.ClassifyTransportFailure(result, responseCode, error), Is.EqualTo(expected));
        }

        [Test]
        public void CloudProvider_MapsSuccessOnceAndYieldsOnlyNull()
        {
            var client = CreateControlledChatClient();
            client.RoutineFactory = (onSuccess, onError) => ControlledChatEnumerator.SuccessTwice(new ChatResponse
            {
                animalId = "sensen",
                reply = "Cloud reply.",
                source = "cloud_llm",
                contentAuthority = "none",
                languageGenerator = "cloud_llm",
                routeReason = "server_reason",
                suggestedQuestions = new[] { "Why?" },
                missionHint = "Protect forest.",
                answerMode = "grounded_fact",
                evidenceStatus = "evidence_found",
                citations = new[]
                {
                    new ChatCitation
                    {
                        sourceId = "iucn-2020-s-priam",
                        title = "IUCN assessment",
                        organization = "IUCN Red List",
                        url = "https://example.test/iucn"
                    }
                }
            }, onSuccess);
            var provider = new CloudLLMProvider(client);
            AIResponse response = null;
            var callbacks = 0;

            RunProviderStrict(provider.Send(Request(), 7.5f, value =>
            {
                callbacks++;
                response = value;
            }, Fail));

            Assert.That(client.LastTimeoutSeconds, Is.EqualTo(7.5f));
            Assert.That(callbacks, Is.EqualTo(1));
            Assert.That(response.animalId, Is.EqualTo("sensen"));
            Assert.That(response.reply, Is.EqualTo("Cloud reply."));
            Assert.That(response.source, Is.EqualTo("cloud_llm"));
            Assert.That(response.routeReason, Is.Null);
            Assert.That(response.suggestedQuestions, Is.EqualTo(new[] { "Why?" }));
            Assert.That(response.answerMode, Is.EqualTo("grounded_fact"));
            Assert.That(response.evidenceStatus, Is.EqualTo("evidence_found"));
            Assert.That(response.citations, Has.Length.EqualTo(1));
            Assert.That(response.citations[0].sourceId, Is.EqualTo("iucn-2020-s-priam"));
        }

        [Test]
        public void CloudProvider_TraversesTheRealChatClientOverloadChain()
        {
            var gameObject = new GameObject("DeepControlledChatApiClientTests");
            createdObjects.Add(gameObject);
            var client = gameObject.AddComponent<DeepControlledChatApiClient>();
            var provider = new CloudLLMProvider(client);
            AIResponse response = null;

            RunProviderStrict(provider.Send(Request(), 7f, value => response = value, Fail));

            Assert.That(response, Is.Not.Null);
            Assert.That(response.source, Is.EqualTo("cloud_llm"));
            Assert.That(client.DeepestOverloadCalls, Is.EqualTo(1));
        }

        [Test]
        public void CloudProvider_ForwardsTheRequestReadOnlyContext()
        {
            var client = CreateControlledChatClient();
            var provider = new CloudLLMProvider(client);
            var request = Request();
            request.Context = ReadOnlyCharacterContext.Create(
                new ReadOnlyCharacterState("sensen", true, 1, 1),
                new ReadOnlyTaskState("food-mission", "帮森森寻找食物", true),
                ReadOnlyInteractionState.Empty);

            RunProviderStrict(provider.Send(request, 7f, Ignore, Fail));

            Assert.That(client.LastContext, Is.SameAs(request.Context));
        }

        [Test]
        public void LocalProvider_PayloadIncludesTheSameReadOnlyContext()
        {
            var request = Request();
            request.Context = ReadOnlyCharacterContext.Create(
                new ReadOnlyCharacterState("sensen", true, 1, 1),
                new ReadOnlyTaskState("food-mission", "帮森森寻找食物", true),
                ReadOnlyInteractionState.Empty);

            var payload = LocalLLMProvider.CreatePayload(request);

            Assert.That(payload.context, Is.SameAs(request.Context));
            Assert.That(payload.animalId, Is.EqualTo("sensen"));
        }

        [TestCase("taunt", AIAction.Taunt)]
        [TestCase("none", AIAction.None)]
        [TestCase(null, AIAction.None)]
        [TestCase("TAUNT", AIAction.None)]
        [TestCase(" taunt", AIAction.None)]
        [TestCase("taunt ", AIAction.None)]
        [TestCase("taunt;delete", AIAction.None)]
        [TestCase("eat", AIAction.None)]
        [TestCase("Eat", AIAction.None)]
        [TestCase("EAT", AIAction.None)]
        [TestCase("eat ", AIAction.None)]
        [TestCase("eat;delete", AIAction.None)]
        public void CloudProvider_MapsTransportActionWithStrictParser(string raw, AIAction expected)
        {
            var response = CloudLLMProvider.ToAIResponse(new ChatResponse
            {
                animalId = "sensen",
                reply = "Reply.",
                source = "cloud_llm",
                contentAuthority = "none",
                languageGenerator = "cloud_llm",
                actionSuggestion = raw
            });

            Assert.That(response.ActionSuggestion, Is.EqualTo(expected));
        }

        [Test]
        public void CloudProvider_MapsGroundingMetadataAndStablyDeduplicatesFactIds()
        {
            var response = CloudLLMProvider.ToAIResponse(new ChatResponse
            {
                animalId = "sensen",
                reply = "Reply.",
                source = "cloud_llm",
                contentAuthority = "none",
                languageGenerator = "cloud_llm",
                groundingTopic = "diet",
                groundedFactIds = new[] { "sensen.diet", "", "sensen.diet", null }
            });

            Assert.That(response.GroundingTopic, Is.EqualTo(GroundingTopic.Diet));
            Assert.That(response.GroundedFactIds, Is.EqualTo(new[] { "sensen.diet" }));
        }

        [Test]
        public void CloudProvider_MapsExecutionProvenanceWithoutGuessingFromReply()
        {
            var response = CloudLLMProvider.ToAIResponse(new ChatResponse
            {
                animalId = "sensen",
                reply = "Same text could come from any route.",
                source = "cloud_llm",
                contentAuthority = "none",
                languageGenerator = "cloud_llm",
                providerAttempt = "cloud_llm",
                fallbackUsed = false,
                fallbackReason = "",
                elapsedMs = 87
            });

            Assert.That(response, Is.Not.Null);
            Assert.That(response.source, Is.EqualTo("cloud_llm"));
            Assert.That(response.ProviderAttempts, Is.EqualTo(new[] { "cloud_llm" }));
            Assert.That(response.FallbackUsed, Is.False);
            Assert.That(response.FallbackReasonCode, Is.Empty);
            Assert.That(response.ElapsedMilliseconds, Is.EqualTo(87));
            Assert.That(response.LanguageGenerator, Is.EqualTo(LanguageGenerator.CloudLlm));
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("cloud")]
        [TestCase(" Cloud_llm")]
        public void CloudProvider_RejectsMissingOrMalformedFinalSource(string source)
        {
            Assert.That(CloudLLMProvider.ToAIResponse(new ChatResponse
            {
                animalId = "sensen",
                reply = "Reply.",
                source = source
            }), Is.Null);
        }

        [Test]
        public void CloudProvider_ClassifiesTimeoutErrors()
        {
            var client = CreateControlledChatClient();
            client.RoutineFactory = (onSuccess, onError) => ControlledChatEnumerator.Error("Request timeout after 7 seconds.", onError);
            var provider = new CloudLLMProvider(client);
            AIProviderError error = null;

            RunProviderStrict(provider.Send(Request(), 7f, Ignore, value => error = value));

            Assert.That(error, Is.Not.Null);
            Assert.That(error.Code, Is.EqualTo("cloud_timeout"));
            Assert.That(error.IsTimeout, Is.True);
        }

        [Test]
        public void CloudProvider_PreservesStructuredTransportErrorCode()
        {
            var client = CreateControlledChatClient();
            client.RoutineFactory = (onSuccess, onError) =>
                ControlledChatEnumerator.Error("cloud_connection_failed", onError);
            var provider = new CloudLLMProvider(client);
            AIProviderError error = null;

            RunProviderStrict(provider.Send(Request(), 7f, Ignore, value => error = value));

            Assert.That(error, Is.Not.Null);
            Assert.That(error.Code, Is.EqualTo("cloud_connection_failed"));
            Assert.That(error.IsTimeout, Is.False);
        }

        [Test]
        public void CloudProvider_ReportsSanitizedRoutineExceptionStage()
        {
            var client = CreateControlledChatClient();
            client.RoutineFactory = (onSuccess, onError) => new ThrowingChatEnumerator();
            var provider = new CloudLLMProvider(client);
            AIProviderError error = null;

            RunProviderStrict(provider.Send(Request(), 7f, Ignore, value => error = value));

            Assert.That(error, Is.Not.Null);
            Assert.That(error.Code, Is.EqualTo("cloud_routine_invalid_operation"));
        }

        [Test]
        public void CloudProvider_RejectsOpaqueClientYieldWithSpecificCode()
        {
            var client = CreateControlledChatClient();
            client.RoutineFactory = (onSuccess, onError) => new OpaqueChatEnumerator();
            var provider = new CloudLLMProvider(client);
            AIProviderError error = null;

            RunProviderStrict(provider.Send(Request(), 7f, Ignore, value => error = value));

            Assert.That(error, Is.Not.Null);
            Assert.That(error.Code, Is.EqualTo("cloud_opaque_yield"));
        }

        [Test]
        public void CloudProvider_DisposingOuterEnumeratorDisposesInnerClientEnumerator()
        {
            var client = CreateControlledChatClient();
            var inner = ControlledChatEnumerator.Pending();
            client.RoutineFactory = (onSuccess, onError) => inner;
            var provider = new CloudLLMProvider(client);
            var routine = provider.Send(Request(), 7f, Ignore, Fail);

            Assert.That(routine.MoveNext(), Is.True);
            Assert.That(routine.Current, Is.Null);
            ((IDisposable)routine).Dispose();

            Assert.That(inner.Disposed, Is.True);
        }

        [Test]
        public void LocalProvider_MissingUrlReportsConfigurationFailure()
        {
            var provider = new LocalLLMProvider(" ");
            AIProviderError error = null;

            RunProviderStrict(provider.Send(Request(), 8f, Ignore, value => error = value));

            Assert.That(error, Is.Not.Null);
            Assert.That(error.Code, Is.EqualTo("local_configuration_error"));
            Assert.That(error.IsTimeout, Is.False);
        }

        [Test]
        public void LocalProvider_BuildsStableLocalEndpointAndRejectsUnsafeBaseUrls()
        {
            string endpoint;

            Assert.That(LocalLLMProvider.TryBuildEndpoint("http://127.0.0.1:8000/", out endpoint), Is.True);
            Assert.That(endpoint, Is.EqualTo("http://127.0.0.1:8000/chat/local"));
            Assert.That(LocalLLMProvider.TryBuildEndpoint("file:///tmp/local-ai", out endpoint), Is.False);
            Assert.That(endpoint, Is.Null);
            Assert.That(LocalLLMProvider.TryBuildEndpoint("http://127.0.0.1:8000?token=one", out endpoint), Is.False);
            Assert.That(endpoint, Is.Null);
            Assert.That(LocalLLMProvider.TryBuildEndpoint("http://127.0.0.1:8000#fragment", out endpoint), Is.False);
            Assert.That(endpoint, Is.Null);
        }

        [Test]
        public void LocalProvider_ParsesStableResponseMappingWithoutNetwork()
        {
            AIResponse response;
            var parsed = LocalLLMProvider.TryParseResponse(
                Request(),
                "{\"animalId\":\"sensen\",\"reply\":\"Local reply.\",\"source\":\"local_llm\",\"contentAuthority\":\"none\",\"languageGenerator\":\"local_llm\",\"answerMode\":\"grounded_fact\",\"evidenceStatus\":\"evidence_found\",\"citations\":[{\"sourceId\":\"gbif-4267223\",\"title\":\"GBIF taxon\",\"organization\":\"GBIF\",\"url\":\"https://example.test/gbif\"}],\"suggestedQuestions\":[\"How?\"],\"missionHint\":\"Protect habitat.\"}",
                out response);

            Assert.That(parsed, Is.True);
            Assert.That(response.animalId, Is.EqualTo("sensen"));
            Assert.That(response.reply, Is.EqualTo("Local reply."));
            Assert.That(response.source, Is.EqualTo("local_llm"));
            Assert.That(response.routeReason, Is.Null);
            Assert.That(response.suggestedQuestions, Is.EqualTo(new[] { "How?" }));
            Assert.That(response.answerMode, Is.EqualTo("grounded_fact"));
            Assert.That(response.evidenceStatus, Is.EqualTo("evidence_found"));
            Assert.That(response.citations[0].organization, Is.EqualTo("GBIF"));
        }

        [Test]
        public void LocalProvider_ResponseWithoutOptionalGroundingFieldsRemainsReadable()
        {
            AIResponse response;

            var parsed = LocalLLMProvider.TryParseResponse(
                Request(),
                "{\"animalId\":\"sensen\",\"reply\":\"Legacy reply.\",\"source\":\"local_llm\",\"contentAuthority\":\"none\",\"languageGenerator\":\"local_llm\"}",
                out response);

            Assert.That(parsed, Is.True);
            Assert.That(response.reply, Is.EqualTo("Legacy reply."));
            Assert.That(response.answerMode, Is.Null);
            Assert.That(response.evidenceStatus, Is.Null);
            Assert.That(response.citations, Is.Empty);
            Assert.That(response.ActionSuggestion, Is.EqualTo(AIAction.None));
            Assert.That(response.GroundingTopic, Is.EqualTo(GroundingTopic.None));
            Assert.That(response.GroundedFactIds, Is.Empty);
        }

        [TestCase("{\"animalId\":\"sensen\",\"reply\":\"Reply.\"}")]
        [TestCase("{\"animalId\":\"sensen\",\"reply\":\"Reply.\",\"source\":\"local\"}")]
        [TestCase("{\"animalId\":\"sensen\",\"reply\":\"Reply.\",\"source\":\" local_llm\"}")]
        public void LocalProvider_RejectsMissingOrMalformedFinalSource(string json)
        {
            Assert.That(LocalLLMProvider.TryParseResponse(Request(), json, out _), Is.False);
        }

        [Test]
        public void LocalProvider_UsesSameStrictGroundingMappingAndFactIdDeduplication()
        {
            AIResponse response;
            const string json = "{\"animalId\":\"sensen\",\"reply\":\"Reply.\",\"source\":\"local_llm\",\"contentAuthority\":\"none\",\"languageGenerator\":\"local_llm\",\"groundingTopic\":\"diet\",\"groundedFactIds\":[\"sensen.diet\",\"sensen.diet\",\"\"]}";

            Assert.That(LocalLLMProvider.TryParseResponse(Request(), json, out response), Is.True);
            Assert.That(response.GroundingTopic, Is.EqualTo(GroundingTopic.Diet));
            Assert.That(response.GroundedFactIds, Is.EqualTo(new[] { "sensen.diet" }));
        }

        [TestCase("taunt", AIAction.Taunt)]
        [TestCase("none", AIAction.None)]
        [TestCase("TAUNT", AIAction.None)]
        [TestCase("taunt ", AIAction.None)]
        [TestCase("taunt_now", AIAction.None)]
        [TestCase("eat", AIAction.None)]
        [TestCase("Eat", AIAction.None)]
        [TestCase("EAT", AIAction.None)]
        [TestCase("eat ", AIAction.None)]
        [TestCase("eat;delete", AIAction.None)]
        public void LocalProvider_MapsTransportActionWithSameStrictParser(string raw, AIAction expected)
        {
            AIResponse response;
            var json = $"{{\"animalId\":\"sensen\",\"reply\":\"Reply.\",\"source\":\"local_llm\",\"contentAuthority\":\"none\",\"languageGenerator\":\"local_llm\",\"actionSuggestion\":\"{raw}\"}}";

            Assert.That(LocalLLMProvider.TryParseResponse(Request(), json, out response), Is.True);
            Assert.That(response.ActionSuggestion, Is.EqualTo(expected));
        }

        [TestCase("local_llm_not_configured", false)]
        [TestCase("local_llm_invalid_configuration", false)]
        [TestCase("local_llm_timeout", true)]
        public void LocalProvider_ParsesStablePythonErrorCode(string code, bool isTimeout)
        {
            AIProviderError error;
            var parsed = LocalLLMProvider.TryParseErrorResponse($"{{\"error\":\"{code}\"}}", out error);

            Assert.That(parsed, Is.True);
            Assert.That(error.Code, Is.EqualTo(code));
            Assert.That(error.IsTimeout, Is.EqualTo(isTimeout));
        }

        [Test]
        public void ChatApiClient_AbortAndDisposeAlwaysDisposesAfterAbortFailure()
        {
            var calls = new List<string>();

            Assert.Throws<InvalidOperationException>(() => ChatApiClient.AbortAndDispose(
                () =>
                {
                    calls.Add("abort");
                    throw new InvalidOperationException("abort failed");
                },
                () => calls.Add("dispose")));

            Assert.That(calls, Is.EqualTo(new[] { "abort", "dispose" }));
        }

        [Test]
        public void LocalKnowledgeProvider_MapsServiceAnswerToUnifiedKnowledgeSource()
        {
            var gameObject = new GameObject("LocalKnowledgeProviderTests");
            createdObjects.Add(gameObject);
            var provider = new LocalKnowledgeProvider(gameObject.AddComponent<LocalKnowledgeChatService>());
            AIResponse response = null;

            RunProviderStrict(provider.Send(Request(), 0f, value => response = value, Fail));

            Assert.That(response, Is.Not.Null);
            Assert.That(response.source, Is.EqualTo("unity_fallback"));
            Assert.That(response.routeReason, Is.Null);
            Assert.That(response.reply, Is.EqualTo(ChatAnswer.GenericFallback.Reply));
            Assert.That(response.citations, Is.Empty);
            Assert.That(response.ActionSuggestion, Is.EqualTo(AIAction.None));
        }

        [Test]
        public void LocalKnowledgeProvider_ComputesTypedActionFromOriginalRequestOnly()
        {
            var gameObject = new GameObject("LocalKnowledgeActionTests");
            createdObjects.Add(gameObject);
            var provider = new LocalKnowledgeProvider(gameObject.AddComponent<LocalKnowledgeChatService>());
            var request = Request();
            request.message = "森森，给我表演一下";
            AIResponse response = null;

            RunProviderStrict(provider.Send(request, 0f, value => response = value, Fail));

            Assert.That(response.ActionSuggestion, Is.EqualTo(AIAction.Taunt));
        }

        [Test]
        public void LocalKnowledgeProvider_DietQuestionReturnsTrustedMetadataButNoTransportEat()
        {
            var gameObject = new GameObject("LocalKnowledgeDietActionTests");
            createdObjects.Add(gameObject);
            var provider = new LocalKnowledgeProvider(gameObject.AddComponent<LocalKnowledgeChatService>());
            var request = Request();
            request.message = "森森，你平时吃什么？";
            request.knowledgeProfile = Resources.Load<EndangeredAR.Animals.AnimalKnowledgeProfile>("Animals/SensenKnowledge");
            AIResponse response = null;

            RunProviderStrict(provider.Send(request, 0f, value => response = value, Fail));

            Assert.That(response.answerMode, Is.EqualTo("grounded_fact"));
            Assert.That(response.evidenceStatus, Is.EqualTo("evidence_found"));
            Assert.That(response.GroundingTopic, Is.EqualTo(GroundingTopic.Diet));
            Assert.That(response.GroundedFactIds, Is.EqualTo(new[] { "sensen.diet" }));
            Assert.That(response.citations, Is.Not.Empty);
            Assert.That(response.ActionSuggestion, Is.EqualTo(AIAction.None));
        }

        [Test]
        public void LocalKnowledgeProvider_PreciseDietQuantityHasNoGroundingAuthority()
        {
            var gameObject = new GameObject("LocalKnowledgePreciseDietTests");
            createdObjects.Add(gameObject);
            var provider = new LocalKnowledgeProvider(gameObject.AddComponent<LocalKnowledgeChatService>());
            var request = Request();
            request.message = "你每天准确吃多少克叶子？";
            request.knowledgeProfile = Resources.Load<EndangeredAR.Animals.AnimalKnowledgeProfile>("Animals/SensenKnowledge");
            AIResponse response = null;

            RunProviderStrict(provider.Send(request, 0f, value => response = value, Fail));

            Assert.That(response.evidenceStatus, Is.EqualTo("insufficient_evidence"));
            Assert.That(response.GroundingTopic, Is.EqualTo(GroundingTopic.None));
            Assert.That(response.GroundedFactIds, Is.Empty);
            Assert.That(response.ActionSuggestion, Is.EqualTo(AIAction.None));
        }

        [Test]
        public void LocalKnowledgeProvider_ResolvesOnlyCanonicalSourceIds()
        {
            var gameObject = new GameObject("LocalKnowledgeProviderCanonicalTests");
            createdObjects.Add(gameObject);
            var provider = new LocalKnowledgeProvider(gameObject.AddComponent<LocalKnowledgeChatService>());
            var request = Request();
            request.message = "你的学名是什么？";
            request.knowledgeProfile = Resources.Load<EndangeredAR.Animals.AnimalKnowledgeProfile>("Animals/SensenKnowledge");
            AIResponse response = null;

            RunProviderStrict(provider.Send(request, 0f, value => response = value, Fail));

            Assert.That(response.evidenceStatus, Is.EqualTo("evidence_found"));
            Assert.That(response.citations, Has.Length.EqualTo(2));
            Assert.That(response.citations[0].sourceId, Is.EqualTo("gbif-4267223"));
            Assert.That(response.citations[1].sourceId, Is.EqualTo("mdd-1000692"));
        }

        [Test]
        public void AIManager_MissingConfigReturnsLocalUnavailableWithoutKnowledgeReply()
        {
            var gameObject = new GameObject("AIManagerTests");
            createdObjects.Add(gameObject);
            var manager = gameObject.AddComponent<AIManager>();
            var knowledgeService = gameObject.AddComponent<LocalKnowledgeChatService>();
            var serializedManager = new SerializedObject(manager);
            serializedManager.FindProperty("localKnowledgeService").objectReferenceValue = knowledgeService;
            serializedManager.ApplyModifiedPropertiesWithoutUndo();
            AIResponse response = null;
            AIProviderError providerError = null;

            Assert.DoesNotThrow(() => RunCoroutine(manager.Send(
                Request(),
                value => response = value,
                value => providerError = value)));
            Assert.That(response, Is.Null);
            Assert.That(providerError, Is.Not.Null);
            Assert.That(providerError.Code, Is.EqualTo("local_model_unavailable"));
        }

        private static AIRequest Request()
        {
            return new AIRequest
            {
                requestId = "request-1",
                animalId = "sensen",
                message = "How are you?",
                history = Array.Empty<ChatMessage>()
            };
        }

        private ControlledChatApiClient CreateControlledChatClient()
        {
            var gameObject = new GameObject("ControlledChatApiClientTests");
            createdObjects.Add(gameObject);
            return gameObject.AddComponent<ControlledChatApiClient>();
        }

        private static void RunProviderStrict(IEnumerator routine)
        {
            while (routine.MoveNext())
            {
                Assert.That(routine.Current, Is.Null, "IAIProvider root enumerators may yield only null while pending.");
            }
        }

        private static void RunCoroutine(IEnumerator routine)
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
                    continue;
                }

            }
        }

        private static void Ignore(AIResponse response)
        {
        }

        private static void IgnoreChatResponse(ChatResponse response)
        {
        }

        private static void FailChat(string error)
        {
            Assert.Fail($"Unexpected chat client error: {error}");
        }

        private static void Fail(AIProviderError error)
        {
            Assert.Fail($"Unexpected provider error: {error?.Code}");
        }

        private sealed class ControlledChatApiClient : ChatApiClient
        {
            public Func<Action<ChatResponse>, Action<string>, IEnumerator> RoutineFactory { get; set; }
            public float LastTimeoutSeconds { get; private set; }
            public ChatMessage[] LastHistory { get; private set; }
            public ReadOnlyCharacterContext LastContext { get; private set; }

            public override IEnumerator SendMessage(
                string animalId,
                string message,
                ChatMessage[] history,
                ReadOnlyCharacterContext context,
                ReadOnlyCharacterMemoryContext memoryContext,
                MemoryUseMode memoryUseMode,
                ContentAuthority contentAuthority,
                float timeoutSeconds,
                Action<ChatResponse> onSuccess,
                Action<string> onError)
            {
                LastContext = context;
                return SendMessage(animalId, message, history, timeoutSeconds, onSuccess, onError);
            }

            public override IEnumerator SendMessage(
                string animalId,
                string message,
                ChatMessage[] history,
                float timeoutSeconds,
                Action<ChatResponse> onSuccess,
                Action<string> onError)
            {
                LastTimeoutSeconds = timeoutSeconds;
                LastHistory = history;
                return RoutineFactory?.Invoke(onSuccess, onError) ?? ControlledChatEnumerator.Success(new ChatResponse
                {
                    animalId = animalId,
                    reply = "Default reply.",
                    source = "cloud_llm",
                    contentAuthority = "none",
                    languageGenerator = "cloud_llm"
                }, onSuccess);
            }
        }

        private sealed class DeepControlledChatApiClient : ChatApiClient
        {
            public int DeepestOverloadCalls { get; private set; }

            public override IEnumerator SendMessage(
                string animalId,
                string message,
                ChatMessage[] history,
                ReadOnlyCharacterContext context,
                ReadOnlyCharacterMemoryContext memoryContext,
                MemoryUseMode memoryUseMode,
                ContentAuthority contentAuthority,
                float timeoutSeconds,
                Action<ChatResponse> onSuccess,
                Action<string> onError)
            {
                DeepestOverloadCalls++;
                return ControlledChatEnumerator.Success(new ChatResponse
                {
                    animalId = animalId,
                    reply = "Cloud reply.",
                    source = "cloud_llm",
                    contentAuthority = ContentAuthorityProtocol.ToWireValue(contentAuthority),
                    languageGenerator = "cloud_llm"
                }, onSuccess);
            }
        }

        private sealed class ControlledChatEnumerator : IEnumerator, IDisposable
        {
            private readonly Action<ChatResponse> onSuccess;
            private readonly Action<string> onError;
            private readonly ChatResponse response;
            private readonly string error;
            private readonly bool invokeTwice;
            private readonly bool staysPending;
            private bool invoked;

            private ControlledChatEnumerator(
                ChatResponse response,
                string error,
                bool invokeTwice,
                bool staysPending,
                Action<ChatResponse> onSuccess = null,
                Action<string> onError = null)
            {
                this.response = response;
                this.error = error;
                this.invokeTwice = invokeTwice;
                this.staysPending = staysPending;
                this.onSuccess = onSuccess;
                this.onError = onError;
            }

            public bool Disposed { get; private set; }
            public object Current => null;

            public static ControlledChatEnumerator Success(ChatResponse response, Action<ChatResponse> onSuccess)
            {
                return new ControlledChatEnumerator(response, null, false, false, onSuccess);
            }

            public static ControlledChatEnumerator SuccessTwice(ChatResponse response, Action<ChatResponse> onSuccess)
            {
                return new ControlledChatEnumerator(response, null, true, false, onSuccess);
            }

            public static ControlledChatEnumerator Error(string error, Action<string> onError)
            {
                return new ControlledChatEnumerator(null, error, false, false, null, onError);
            }

            public static ControlledChatEnumerator Pending()
            {
                return new ControlledChatEnumerator(null, null, false, true);
            }

            public bool MoveNext()
            {
                if (staysPending)
                {
                    return true;
                }

                if (invoked)
                {
                    return false;
                }

                invoked = true;
                if (response != null)
                {
                    onSuccess?.Invoke(response);
                    if (invokeTwice)
                    {
                        onSuccess?.Invoke(response);
                    }
                }
                else
                {
                    onError?.Invoke(error);
                }

                return false;
            }

            public void Reset()
            {
                throw new NotSupportedException();
            }

            public void Dispose()
            {
                Disposed = true;
            }
        }

        private sealed class ThrowingChatEnumerator : IEnumerator
        {
            public object Current => null;

            public bool MoveNext()
            {
                throw new InvalidOperationException("Sensitive implementation detail");
            }

            public void Reset()
            {
                throw new NotSupportedException();
            }
        }

        private sealed class OpaqueChatEnumerator : IEnumerator
        {
            private bool yielded;

            public object Current => new object();

            public bool MoveNext()
            {
                if (yielded)
                {
                    return false;
                }

                yielded = true;
                return true;
            }

            public void Reset()
            {
                throw new NotSupportedException();
            }
        }
    }
}
