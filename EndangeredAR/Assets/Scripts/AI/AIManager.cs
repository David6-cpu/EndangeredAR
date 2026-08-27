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
        private IOnDeviceLLMProvider ownedOnDeviceProvider;
        private IOnDeviceLLMProvider activeOnDeviceProvider;

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
            ResolveProviders(
                config,
                out var localProvider,
                out var cloudProvider,
                out var routeMode);
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
                if (error != null)
                {
                    error.ContentAuthority = request == null ? ContentAuthority.None : request.ContentAuthority;
                    error.MemoryMentionMode = mentionMode;
                    error.ProvenanceMemoryStatus = memoryContext == null
                        ? "not_read"
                        : CharacterMemoryContextStatusProtocol.ToWireValue(memoryContext.Status);
                }

                onError?.Invoke(error);
            };

            yield return router.Route(
                request,
                routeMode,
                config == null ? DefaultLocalTimeoutSeconds : config.localTimeoutSeconds,
                config == null ? DefaultTotalTimeoutSeconds : config.totalTimeoutSeconds,
                routeSuccess,
                routeError);
        }

        private void ResolveProviders(
            AIConfig config,
            out IAIProvider localProvider,
            out IAIProvider cloudProvider,
            out AIRouteMode routeMode)
        {
            if (onDeviceProviderOverride != null)
            {
                activeOnDeviceProvider = onDeviceProviderOverride;
                localProvider = CreateOnDeviceComposer(onDeviceProviderOverride);
                cloudProvider = null;
                routeMode = AIRouteMode.LocalOnly;
                return;
            }

            if (localProviderOverride != null || cloudProviderOverride != null)
            {
                activeOnDeviceProvider = null;
                localProvider = localProviderOverride;
                cloudProvider = cloudProviderOverride;
                routeMode = ResolveRouteMode(config);
                return;
            }

            var configuredMode = config == null ? AIProviderMode.OnDevice : config.providerMode;
            var mode = AIProviderSelection.Resolve(configuredMode, DevelopmentRoutesAllowed);
            switch (mode)
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                case AIProviderMode.DevelopmentRemote:
                    activeOnDeviceProvider = null;
                    localProvider = new DevelopmentRemoteLLMProvider(
                        config == null ? string.Empty : config.developmentRemoteServerUrl);
                    cloudProvider = null;
                    routeMode = AIRouteMode.LocalOnly;
                    return;
                case AIProviderMode.DevelopmentCloud:
                    activeOnDeviceProvider = null;
                    localProvider = null;
                    cloudProvider = chatApiClient == null ? null : new CloudLLMProvider(chatApiClient);
                    routeMode = AIRouteMode.CloudOnly;
                    return;
#endif
                case AIProviderMode.OnDevice:
                default:
                    activeOnDeviceProvider = GetOrCreateOnDeviceProvider();
                    localProvider = CreateOnDeviceComposer(activeOnDeviceProvider);
                    cloudProvider = null;
                    routeMode = AIRouteMode.LocalOnly;
                    return;
            }
        }

        private IAIProvider CreateOnDeviceComposer(IOnDeviceLLMProvider provider)
        {
            return new OnDeviceAIResponseComposer(provider, OnDevicePromptBudget.FirstProduction);
        }

        private IOnDeviceLLMProvider GetOrCreateOnDeviceProvider()
        {
            return ownedOnDeviceProvider ??= OnDeviceLLMProviderFactory.CreateProduction();
        }

        private static bool DevelopmentRoutesAllowed
        {
            get
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                return true;
#else
                return false;
#endif
            }
        }

        private void OnEnable()
        {
            Application.lowMemory += HandleLowMemory;
        }

        private void OnDisable()
        {
            Application.lowMemory -= HandleLowMemory;
        }

        private void OnApplicationPause(bool paused)
        {
            activeOnDeviceProvider?.OnApplicationPause(paused);
        }

        private void OnDestroy()
        {
            ownedOnDeviceProvider?.Dispose();
            ownedOnDeviceProvider = null;
            activeOnDeviceProvider = null;
        }

        private void HandleLowMemory()
        {
            var provider = ownedOnDeviceProvider;
            ownedOnDeviceProvider = null;
            if (ReferenceEquals(activeOnDeviceProvider, provider))
            {
                activeOnDeviceProvider = null;
            }

            provider?.Dispose();
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
