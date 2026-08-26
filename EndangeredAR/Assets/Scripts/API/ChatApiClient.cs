using System;
using System.Collections;
using System.Globalization;
using System.Text;
using EndangeredAR.AI;
using UnityEngine;
using UnityEngine.Networking;

namespace EndangeredAR.API
{
    public class ChatApiClient : MonoBehaviour
    {
        [SerializeField] private ApiConfig config;

        public IEnumerator SendMessage(string animalId, string message, Action<ChatResponse> onSuccess, Action<string> onError)
        {
            return SendMessage(animalId, message, Array.Empty<ChatMessage>(), 35f, onSuccess, onError);
        }

        public IEnumerator SendMessage(string animalId, string message, ChatMessage[] history, Action<ChatResponse> onSuccess, Action<string> onError)
        {
            return SendMessage(animalId, message, history, 35f, onSuccess, onError);
        }

        public virtual IEnumerator SendMessage(
            string animalId,
            string message,
            ChatMessage[] history,
            float timeoutSeconds,
            Action<ChatResponse> onSuccess,
            Action<string> onError)
        {
            return SendMessage(
                animalId,
                message,
                history,
                ReadOnlyCharacterContext.Empty,
                timeoutSeconds,
                onSuccess,
                onError);
        }

        public virtual IEnumerator SendMessage(
            string animalId,
            string message,
            ChatMessage[] history,
            ReadOnlyCharacterContext context,
            float timeoutSeconds,
            Action<ChatResponse> onSuccess,
            Action<string> onError)
        {
            return SendMessage(
                animalId,
                message,
                history,
                context,
                null,
                MemoryUseMode.None,
                timeoutSeconds,
                onSuccess,
                onError);
        }

        public virtual IEnumerator SendMessage(
            string animalId,
            string message,
            ChatMessage[] history,
            ReadOnlyCharacterContext context,
            ReadOnlyCharacterMemoryContext memoryContext,
            MemoryUseMode memoryUseMode,
            float timeoutSeconds,
            Action<ChatResponse> onSuccess,
            Action<string> onError)
        {
            if (config == null || string.IsNullOrWhiteSpace(config.baseUrl))
            {
                onError?.Invoke("cloud_configuration_error");
                yield break;
            }

            var request = new ChatRequest
            {
                requestId = string.Empty,
                animalId = animalId,
                message = message,
                history = history ?? Array.Empty<ChatMessage>(),
                context = context ?? ReadOnlyCharacterContext.Empty,
                memoryUseMode = MemoryUseModeProtocol.ToWireValue(memoryUseMode),
                memoryContext = CharacterMemoryTransport.SelectContext(animalId, memoryContext, memoryUseMode)
            };

            var json = JsonUtility.ToJson(request);
            var url = $"{config.baseUrl.TrimEnd('/')}/chat";
            var webRequest = new UnityWebRequest(url, "POST");
            try
            {
                webRequest.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
                webRequest.downloadHandler = new DownloadHandlerBuffer();
                webRequest.timeout = ToUnityTimeoutSeconds(timeoutSeconds);
                webRequest.SetRequestHeader("Content-Type", "application/json");

                var operation = webRequest.SendWebRequest();
                while (!operation.isDone)
                {
                    yield return null;
                }

                if (webRequest.result != UnityWebRequest.Result.Success)
                {
                    onError?.Invoke(ClassifyTransportFailure(
                        webRequest.result,
                        webRequest.responseCode,
                        webRequest.error));
                    yield break;
                }

                var response = JsonUtility.FromJson<ChatResponse>(webRequest.downloadHandler.text);
                if (response == null || string.IsNullOrWhiteSpace(response.reply))
                {
                    onError?.Invoke("cloud_invalid_response");
                    yield break;
                }

                onSuccess?.Invoke(response);
            }
            finally
            {
                AbortAndDispose(webRequest);
            }
        }

        internal static int ToUnityTimeoutSeconds(float timeoutSeconds)
        {
            if (float.IsNaN(timeoutSeconds) || float.IsInfinity(timeoutSeconds) || timeoutSeconds <= 0f)
            {
                return 1;
            }

            return timeoutSeconds >= int.MaxValue ? int.MaxValue : Mathf.Max(1, Mathf.CeilToInt(timeoutSeconds));
        }

        internal static string ClassifyTransportFailure(
            UnityWebRequest.Result result,
            long responseCode,
            string error)
        {
            if (Contains(error, "timeout") || Contains(error, "timed out"))
            {
                return "cloud_timeout";
            }

            switch (result)
            {
                case UnityWebRequest.Result.ConnectionError:
                    if (Contains(error, "resolve"))
                    {
                        return "cloud_dns_failed";
                    }

                    if (Contains(error, "local network"))
                    {
                        return "cloud_local_network_denied";
                    }

                    if (Contains(error, "insecure connection") || Contains(error, "app transport security"))
                    {
                        return "cloud_transport_security_blocked";
                    }

                    return "cloud_connection_failed";

                case UnityWebRequest.Result.ProtocolError:
                    return responseCode > 0L
                        ? $"cloud_http_{responseCode.ToString(CultureInfo.InvariantCulture)}"
                        : "cloud_http_error";

                case UnityWebRequest.Result.DataProcessingError:
                    return "cloud_data_processing_error";

                default:
                    return "cloud_request_failed";
            }
        }

        private static bool Contains(string value, string expected)
        {
            return !string.IsNullOrEmpty(value) &&
                value.IndexOf(expected, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        internal static void AbortAndDispose(UnityWebRequest webRequest)
        {
            if (webRequest == null)
            {
                return;
            }

            AbortAndDispose(webRequest.Abort, webRequest.Dispose);
        }

        internal static void AbortAndDispose(Action abort, Action dispose)
        {
            try
            {
                abort?.Invoke();
            }
            finally
            {
                dispose?.Invoke();
            }
        }
    }

    [Serializable]
    public class ChatRequest
    {
        public string requestId;
        public string animalId;
        public string message;
        public ChatMessage[] history;
        public ReadOnlyCharacterContext context;
        public string memoryUseMode;
        public ReadOnlyCharacterMemoryContext memoryContext;
    }

    [Serializable]
    public class ChatMessage
    {
        public string role;
        public string content;
    }

    [Serializable]
    public class ChatResponse
    {
        public string animalId;
        public string reply;
        public string source;
        public string routeReason;
        public string providerAttempt;
        public bool fallbackUsed;
        public string fallbackReason;
        public long elapsedMs;
        public string[] suggestedQuestions;
        public string missionHint;
        public string answerMode;
        public string evidenceStatus;
        public string groundingTopic;
        public string[] groundedFactIds = Array.Empty<string>();
        public string actionSuggestion;
        public ChatCitation[] citations = Array.Empty<ChatCitation>();
    }

    [Serializable]
    public class ChatCitation
    {
        public string sourceId;
        public string title;
        public string organization;
        public string url;
    }
}
