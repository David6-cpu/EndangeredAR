#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;

namespace EndangeredAR.AI
{
    public sealed class DevelopmentRemoteLLMProvider : IAIProvider
    {
        public const string DevelopmentGeneratorId = "development_remote_llm";
        private readonly IAIProvider transport;

        public DevelopmentRemoteLLMProvider(string serverUrl)
            : this(new LocalLLMProvider(serverUrl))
        {
        }

        internal DevelopmentRemoteLLMProvider(IAIProvider transport)
        {
            this.transport = transport ?? throw new ArgumentNullException(nameof(transport));
        }

        public string ProviderId => DevelopmentGeneratorId;

        public IEnumerator Send(
            AIRequest request,
            float timeoutSeconds,
            Action<AIResponse> onSuccess,
            Action<AIProviderError> onError)
        {
            var routine = transport.Send(
                request,
                timeoutSeconds,
                response =>
                {
                    if (response != null)
                    {
                        response.source = DevelopmentGeneratorId;
                        response.LanguageGenerator = LanguageGenerator.DevelopmentRemoteLlm;
                        response.ProviderAttempts = new[] { DevelopmentGeneratorId };
                    }

                    onSuccess?.Invoke(response);
                },
                onError);
            while (routine != null && routine.MoveNext())
            {
                yield return routine.Current;
            }
        }
    }
}
#endif
