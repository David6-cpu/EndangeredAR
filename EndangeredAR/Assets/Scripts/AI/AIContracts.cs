using System;
using System.Collections;
using EndangeredAR.Animals;
using EndangeredAR.API;

namespace EndangeredAR.AI
{
    public enum AIRouteMode
    {
        CloudOnly,
        LocalOnly,
        LocalFirstCloudFallback
    }

    [Serializable]
    public sealed class AIRequest
    {
        public string requestId;
        public string animalId;
        public string message;
        public ChatMessage[] history;
        [NonSerialized] public AnimalKnowledgeProfile knowledgeProfile;
    }

    [Serializable]
    public sealed class AIResponse
    {
        public string animalId;
        public string reply;
        public string source;
        public string routeReason;
        public string[] suggestedQuestions;
        public string missionHint;
        public string answerMode;
        public string evidenceStatus;
        public GroundingTopic GroundingTopic;
        public string[] GroundedFactIds = Array.Empty<string>();
        public AIAction ActionSuggestion;
        public string emotion;
        public AICitation[] citations = Array.Empty<AICitation>();
    }

    [Serializable]
    public sealed class AICitation
    {
        public string sourceId;
        public string title;
        public string organization;
        public string url;
    }

    public sealed class AIProviderError
    {
        public AIProviderError(string code, string message, bool isTimeout)
        {
            Code = code;
            Message = message;
            IsTimeout = isTimeout;
        }

        public string Code { get; }
        public string Message { get; }
        public bool IsTimeout { get; }
    }

    public interface IAIProvider
    {
        string ProviderId { get; }

        // Send may yield only null while work is pending. AIRouter advances this root
        // enumerator itself so it can enforce its realtime deadline and completion guard.
        IEnumerator Send(
            AIRequest request,
            float timeoutSeconds,
            Action<AIResponse> onSuccess,
            Action<AIProviderError> onError);
    }
}
