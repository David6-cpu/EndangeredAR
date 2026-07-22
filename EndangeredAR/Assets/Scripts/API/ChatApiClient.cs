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
            yield return SendMessage(animalId, message, Array.Empty<ChatMessage>(), onSuccess, onError);
        }

        public IEnumerator SendMessage(string animalId, string message, ChatMessage[] history, Action<ChatResponse> onSuccess, Action<string> onError)
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
            using (var webRequest = new UnityWebRequest(url, "POST"))
            {
                webRequest.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
                webRequest.downloadHandler = new DownloadHandlerBuffer();
                webRequest.timeout = 35;
                webRequest.SetRequestHeader("Content-Type", "application/json");

                yield return webRequest.SendWebRequest();

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
        public string[] suggestedQuestions;
        public string missionHint;
    }
}
