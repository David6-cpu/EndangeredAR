using System;
using System.Collections;
using EndangeredAR.API;
using EndangeredAR.AI.OnDevice;
using EndangeredAR.AI.Prompt;
using EndangeredAR.Chat;
using UnityEngine;

namespace EndangeredAR.AI
{
    public sealed class AIManager : MonoBehaviour
    {
        private const float DefaultLocalTimeoutSeconds = 8f;
        private const float DefaultTotalTimeoutSeconds = 38f;

        [SerializeField] private AIConfig aiConfig;
        [SerializeField] private ChatApiClient chatApiClient;
        [SerializeField] private LocalKnowledgeChatService localKnowledgeService;
        private IReadOnlyCharacterContextProvider contextProvider;
        private IReadOnlyCharacterMemoryContextProvider memoryContextProvider;
        private IAIProvider localProviderOverride;
        private IAIProvider cloudProviderOverride;
        private IOnDeviceLLMProvider onDeviceProviderOverride;

        internal void ConfigureContextProvider(IReadOnlyCharacterContextProvider provider)
        {
            contextProvider = provider;
        }

        internal void ConfigureMemoryContextProvider(IReadOnlyCharacterMemoryContextProvider provider)
        {
            memoryContextProvider = provider;
        }

        internal void ConfigureProviders(IAIProvider localProvider, IAIProvider cloudProvider = null)
        {
            localProviderOverride = localProvider;
            cloudProviderOverride = cloudProvider;
        }

        internal void ConfigureOnDeviceProvider(IOnDeviceLLMProvider provider)
        {
            onDeviceProviderOverride = provider;
        }

        public IEnumerator Send(
            AIRequest request,
            Action<AIResponse> onSuccess,
            Action<AIProviderError> onError)
        {
            var sendStartedAt = Time.realtimeSinceStartup;
            var currentContext = ReadOnlyCharacterContext.Empty;
            if (request != null)
            {
                currentContext = CreateContextSnapshot(request.animalId);
                request.Context = currentContext;
                request.MemoryContext = null;
                request.MemoryUseMode = MemoryUseMode.None;
            }

            var mentionMode = MemoryMentionPolicy.Classify(request?.message);
            ReadOnlyCharacterMemoryContext memoryContext = null;
            if (mentionMode != MemoryMentionMode.None)
            {
                memoryContext = CreateMemoryContextSnapshot(request?.animalId, currentContext);
            }

            if (request != null)
            {
                request.ContentAuthority = ContentAuthorityResolver.Resolve(request, mentionMode);
                request.MemoryUseMode = ResolveMemoryUseMode(mentionMode);
                request.MemoryContext = request.MemoryUseMode == MemoryUseMode.ExplicitRecall ||
                                        request.MemoryUseMode == MemoryUseMode.Reunion
                    ? memoryContext
                    : null;
            }

            var config = aiConfig;
            var localProvider = onDeviceProviderOverride == null
                ? localProviderOverride ?? new LocalLLMProvider(config == null ? null : config.localServerUrl)
                : new OnDeviceAIResponseComposer(
                    onDeviceProviderOverride,
                    OnDevicePromptBudget.FirstProduction);
            var cloudProvider = cloudProviderOverride ?? (chatApiClient == null ? null : new CloudLLMProvider(chatApiClient));
            var router = new AIRouter(localProvider, cloudProvider, null);

            Action<AIResponse> routeSuccess = response =>
            {
                var finalResponse = CharacterMemoryDialogueResolver.PrepareGeneratedResponse(
                    request,
                    response,
                    memoryContext,
                    mentionMode);
                AttachMemoryProvenance(finalResponse, mentionMode, memoryContext);
                onSuccess?.Invoke(finalResponse);
            };
            Action<AIProviderError> routeError = error =>
            {
                error.ContentAuthority = request == null ? ContentAuthority.None : request.ContentAuthority;
                onError?.Invoke(error);
            };

            yield return router.Route(
                request,
                onDeviceProviderOverride == null ? ResolveRouteMode(config) : AIRouteMode.LocalOnly,
                config == null ? DefaultLocalTimeoutSeconds : config.localTimeoutSeconds,
                config == null ? DefaultTotalTimeoutSeconds : config.totalTimeoutSeconds,
                routeSuccess,
                routeError);
        }

        private static void AttachMemoryProvenance(
            AIResponse response,
            MemoryMentionMode mentionMode,
            ReadOnlyCharacterMemoryContext memoryContext)
        {
            if (response == null)
            {
                return;
            }

            response.MemoryMentionMode = mentionMode;
            response.ProvenanceMemoryStatus = memoryContext == null
                ? "not_read"
                : CharacterMemoryContextStatusProtocol.ToWireValue(memoryContext.Status);
        }

        private static long ElapsedMilliseconds(float startedAt)
        {
            return Math.Max(0L, (long)Math.Round((Time.realtimeSinceStartup - startedAt) * 1000f));
        }

        internal AIResponse RefreshMemoryDependentResponse(
            AIResponse response,
            string animalId,
            string originalMessage)
        {
            if (response == null || response.MemoryMentionMode == MemoryMentionMode.None)
            {
                return response;
            }

            var currentContext = CreateContextSnapshot(animalId);
            var memoryContext = CreateMemoryContextSnapshot(animalId, currentContext);
            return CharacterMemoryDialogueResolver.IsFresh(
                    response,
                    animalId,
                    originalMessage,
                    memoryContext)
                ? response
                : null;
        }

        private static MemoryUseMode ResolveMemoryUseMode(MemoryMentionMode mentionMode)
        {
            switch (mentionMode)
            {
                case MemoryMentionMode.ExplicitRecall:
                    return MemoryUseMode.ExplicitRecall;
                case MemoryMentionMode.ConversationHistoryBoundary:
                    return MemoryUseMode.HistoryBoundary;
                case MemoryMentionMode.Reunion:
                    return MemoryUseMode.Reunion;
                default:
                    return MemoryUseMode.None;
            }
        }

        private static AIRouteMode ResolveRouteMode(AIConfig config)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            return config == null ? AIRouteMode.LocalOnly : config.routeMode;
#else
            return AIRouteMode.LocalOnly;
#endif
        }

        private ReadOnlyCharacterContext CreateContextSnapshot(string animalId)
        {
            if (contextProvider == null)
            {
                return ReadOnlyCharacterContext.Empty;
            }

            try
            {
                return contextProvider.CreateSnapshot(animalId) ?? ReadOnlyCharacterContext.Empty;
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"Read-only character context unavailable ({exception.GetType().Name}); continuing without context.", this);
                return ReadOnlyCharacterContext.Empty;
            }
        }

        private ReadOnlyCharacterMemoryContext CreateMemoryContextSnapshot(
            string animalId,
            ReadOnlyCharacterContext currentContext)
        {
            if (memoryContextProvider == null)
            {
                return ReadOnlyCharacterMemoryContext.UnavailableFor(animalId);
            }

            try
            {
                return memoryContextProvider.CreateSnapshot(animalId, currentContext) ??
                       ReadOnlyCharacterMemoryContext.UnavailableFor(animalId);
            }
            catch (Exception exception)
            {
                Debug.LogWarning(
                    $"Read-only character Memory context unavailable ({exception.GetType().Name}); continuing safely.",
                    this);
                return ReadOnlyCharacterMemoryContext.UnavailableFor(animalId);
            }
        }
    }
}
