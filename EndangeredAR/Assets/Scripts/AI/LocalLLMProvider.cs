using System;
using System.Collections;
using System.Text;
using EndangeredAR.API;
using UnityEngine;
using UnityEngine.Networking;

namespace EndangeredAR.AI
{
    public sealed class LocalLLMProvider : IAIProvider
    {
        private readonly string localServerUrl;

        public LocalLLMProvider(string localServerUrl)
        {
            this.localServerUrl = localServerUrl;
        }

        public string ProviderId => "local_llm";

        public IEnumerator Send(
            AIRequest request,
            float timeoutSeconds,
            Action<AIResponse> onSuccess,
            Action<AIProviderError> onError)
        {
            string url;
            if (!TryBuildEndpoint(localServerUrl, out url))
            {
                onError?.Invoke(new AIProviderError("local_configuration_error", "Local AI is not configured.", false));
                yield break;
            }

            var payload = new ChatRequest
            {
                animalId = request == null ? string.Empty : request.animalId,
                message = request == null ? string.Empty : request.message,
                history = request == null || request.history == null ? Array.Empty<ChatMessage>() : request.history
            };

            using (var webRequest = new UnityWebRequest(url, "POST"))
            {
                webRequest.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(JsonUtility.ToJson(payload)));
                webRequest.downloadHandler = new DownloadHandlerBuffer();
                webRequest.timeout = ChatApiClient.ToUnityTimeoutSeconds(timeoutSeconds);
                webRequest.SetRequestHeader("Content-Type", "application/json");

                var operation = webRequest.SendWebRequest();
                while (!operation.isDone)
                {
                    yield return null;
                }

                if (webRequest.result != UnityWebRequest.Result.Success)
                {
                    var isTimeout = IsTimeout(webRequest.error);
                    onError?.Invoke(new AIProviderError(
                        isTimeout ? "local_timeout" : "local_request_failed",
                        isTimeout ? "Local AI request timed out." : "Local AI request failed.",
                        isTimeout));
                    yield break;
                }

                AIResponse response;
                if (!TryParseResponse(request, webRequest.downloadHandler.text, out response))
                {
                    onError?.Invoke(new AIProviderError("local_invalid_response", "Local AI returned an invalid response.", false));
                    yield break;
                }

                onSuccess?.Invoke(response);
            }
        }

        internal static bool TryBuildEndpoint(string serverUrl, out string endpoint)
        {
            endpoint = null;
            if (string.IsNullOrWhiteSpace(serverUrl))
            {
                return false;
            }

            Uri parsed;
            if (!Uri.TryCreate(serverUrl.Trim(), UriKind.Absolute, out parsed) ||
                (parsed.Scheme != Uri.UriSchemeHttp && parsed.Scheme != Uri.UriSchemeHttps))
            {
                return false;
            }

            endpoint = $"{serverUrl.Trim().TrimEnd('/')}/chat/local";
            return true;
        }

        internal static bool TryParseResponse(AIRequest request, string json, out AIResponse response)
        {
            response = null;
            if (string.IsNullOrWhiteSpace(json))
            {
                return false;
            }

            try
            {
                var parsed = JsonUtility.FromJson<ChatResponse>(json);
                if (parsed == null || string.IsNullOrWhiteSpace(parsed.reply))
                {
                    return false;
                }

                response = new AIResponse
                {
                    animalId = string.IsNullOrWhiteSpace(parsed.animalId) ? request?.animalId : parsed.animalId,
                    reply = parsed.reply,
                    source = string.IsNullOrWhiteSpace(parsed.source) ? "local_llm" : parsed.source,
                    suggestedQuestions = parsed.suggestedQuestions,
                    missionHint = parsed.missionHint
                };
                return true;
            }
            catch (ArgumentException)
            {
                return false;
            }
        }

        private static bool IsTimeout(string error)
        {
            return !string.IsNullOrWhiteSpace(error) &&
                error.IndexOf("timeout", StringComparison.OrdinalIgnoreCase) >= 0;
        }
    }
}
