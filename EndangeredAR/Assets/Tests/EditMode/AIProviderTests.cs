using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
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
        public void AIConfig_HasHybridRoutingDefaults()
        {
            var config = ScriptableObject.CreateInstance<AIConfig>();
            createdObjects.Add(config);

            Assert.That(config.routeMode, Is.EqualTo(AIRouteMode.CloudOnly));
            Assert.That(config.localServerUrl, Is.EqualTo("http://127.0.0.1:8000"));
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
        public void CloudProvider_UsesChatApiClientAndPreservesResponseSource()
        {
            var source = File.ReadAllText(Path.GetFullPath("Assets/Scripts/AI/CloudLLMProvider.cs"));
            var mapped = CloudLLMProvider.ToAIResponse(new ChatResponse
            {
                animalId = "sensen",
                reply = "Cloud reply.",
                source = "server_rule",
                routeReason = "server_reason",
                suggestedQuestions = new[] { "Why?" },
                missionHint = "Protect forest."
            });

            StringAssert.Contains("chatApiClient.SendMessage", source);
            StringAssert.DoesNotContain("new UnityWebRequest", source);
            Assert.That(mapped.animalId, Is.EqualTo("sensen"));
            Assert.That(mapped.reply, Is.EqualTo("Cloud reply."));
            Assert.That(mapped.source, Is.EqualTo("server_rule"));
            Assert.That(mapped.routeReason, Is.Null);
            Assert.That(mapped.suggestedQuestions, Is.EqualTo(new[] { "Why?" }));
        }

        [Test]
        public void LocalProvider_MissingUrlReportsConfigurationFailure()
        {
            var provider = new LocalLLMProvider(" ");
            AIProviderError error = null;

            Run(provider.Send(Request(), 8f, Ignore, value => error = value));

            Assert.That(error, Is.Not.Null);
            Assert.That(error.Code, Is.EqualTo("local_configuration_error"));
            Assert.That(error.IsTimeout, Is.False);
        }

        [Test]
        public void LocalProvider_BuildsStableLocalEndpointAndRejectsUnsupportedSchemes()
        {
            string endpoint;

            Assert.That(LocalLLMProvider.TryBuildEndpoint("http://127.0.0.1:8000/", out endpoint), Is.True);
            Assert.That(endpoint, Is.EqualTo("http://127.0.0.1:8000/chat/local"));
            Assert.That(LocalLLMProvider.TryBuildEndpoint("file:///tmp/local-ai", out endpoint), Is.False);
            Assert.That(endpoint, Is.Null);
        }

        [Test]
        public void LocalProvider_ParsesStableResponseMappingWithoutNetwork()
        {
            AIResponse response;
            var parsed = LocalLLMProvider.TryParseResponse(
                Request(),
                "{\"animalId\":\"sensen\",\"reply\":\"Local reply.\",\"source\":\"local_llm\",\"suggestedQuestions\":[\"How?\"],\"missionHint\":\"Protect habitat.\"}",
                out response);

            Assert.That(parsed, Is.True);
            Assert.That(response.animalId, Is.EqualTo("sensen"));
            Assert.That(response.reply, Is.EqualTo("Local reply."));
            Assert.That(response.source, Is.EqualTo("local_llm"));
            Assert.That(response.routeReason, Is.Null);
            Assert.That(response.suggestedQuestions, Is.EqualTo(new[] { "How?" }));
        }

        [Test]
        public void LocalKnowledgeProvider_MapsServiceAnswerToUnifiedKnowledgeSource()
        {
            var gameObject = new GameObject("LocalKnowledgeProviderTests");
            createdObjects.Add(gameObject);
            var provider = new LocalKnowledgeProvider(gameObject.AddComponent<LocalKnowledgeChatService>());
            AIResponse response = null;

            Run(provider.Send(Request(), 0f, value => response = value, Fail));

            Assert.That(response, Is.Not.Null);
            Assert.That(response.source, Is.EqualTo("unity_knowledge"));
            Assert.That(response.routeReason, Is.Null);
            Assert.That(response.reply, Is.EqualTo(ChatAnswer.GenericFallback.Reply));
        }

        [Test]
        public void AIManager_MissingConfigAndHttpClientFallsBackToKnowledgeWithoutThrowing()
        {
            var gameObject = new GameObject("AIManagerTests");
            createdObjects.Add(gameObject);
            var manager = gameObject.AddComponent<AIManager>();
            var knowledgeService = gameObject.AddComponent<LocalKnowledgeChatService>();
            var serializedManager = new SerializedObject(manager);
            serializedManager.FindProperty("localKnowledgeService").objectReferenceValue = knowledgeService;
            serializedManager.ApplyModifiedPropertiesWithoutUndo();
            AIResponse response = null;

            Assert.DoesNotThrow(() => Run(manager.Send(Request(), value => response = value, Fail)));
            Assert.That(response, Is.Not.Null);
            Assert.That(response.source, Is.EqualTo("unity_knowledge"));
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
                    continue;
                }

                Assert.That(current.Current, Is.Null, "Provider root enumerators may yield only null while pending.");
            }
        }

        private static void Ignore(AIResponse response)
        {
        }

        private static void Fail(AIProviderError error)
        {
            Assert.Fail($"Unexpected provider error: {error?.Code}");
        }
    }
}
