using System;
using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

namespace EndangeredAR.API
{
    public class ChatApiClient : MonoBehaviour
    {
        [SerializeField] private ApiConfig config;

        public IEnumerator SendMessage(string animalId, string message, Action<ChatResponse> onSuccess, Action<string> onError)
        {
            yield return SendMessage(animalId, message, Array.Empty<ChatMessage>(), 35f, onSuccess, onError);
        }

        public IEnumerator SendMessage(string animalId, string message, ChatMessage[] history, Action<ChatResponse> onSuccess, Action<string> onError)
        {
            yield return SendMessage(animalId, message, history, 35f, onSuccess, onError);
        }

        public virtual IEnumerator SendMessage(
            string animalId,
            string message,
            ChatMessage[] history,
            float timeoutSeconds,
            Action<ChatResponse> onSuccess,
            Action<string> onError)
        {
            if (config == null || string.IsNullOrWhiteSpace(config.baseUrl))
            {
                onError?.Invoke("API 地址没有配置。请在 Unity 里执行 Endangered AR > Set Local API To Mac LAN IP。");
                yield break;
            }

            var request = new ChatRequest
            {
                animalId = animalId,
                message = message,
                history = history ?? Array.Empty<ChatMessage>()
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
                    onError?.Invoke($"{webRequest.error}\nURL: {url}\nHTTP: {webRequest.responseCode}\n{webRequest.downloadHandler.text}");
                    yield break;
                }

                var response = JsonUtility.FromJson<ChatResponse>(webRequest.downloadHandler.text);
                if (response == null || string.IsNullOrWhiteSpace(response.reply))
                {
                    onError?.Invoke($"云端返回为空。\nURL: {url}\n{webRequest.downloadHandler.text}");
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
        public string animalId;
        public string message;
        public ChatMessage[] history;
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
        public string[] suggestedQuestions;
        public string missionHint;
    }
}
