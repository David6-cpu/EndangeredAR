using System;
using System.Collections;
using System.Collections.Generic;
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
            if (config != null && config.useDirectLlm)
            {
                yield return SendDirectMoonshotMessage(animalId, message, history, onSuccess, onError);
                yield break;
            }

            yield return SendServerMessage(animalId, message, history, onSuccess, onError);
        }

        private IEnumerator SendServerMessage(string animalId, string message, ChatMessage[] history, Action<ChatResponse> onSuccess, Action<string> onError)
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

        private IEnumerator SendDirectMoonshotMessage(string animalId, string message, ChatMessage[] history, Action<ChatResponse> onSuccess, Action<string> onError)
        {
            if (config == null || string.IsNullOrWhiteSpace(config.moonshotApiKey))
            {
                onError?.Invoke("Kimi API Key 没有配置。请在 Unity 里执行 Endangered AR > Set Direct Kimi API From .env.local。");
                yield break;
            }

            var messages = new List<MoonshotMessage>
            {
                new MoonshotMessage
                {
                    role = "system",
                    content = config.EffectiveDirectLlmSystemPrompt
                }
            };

            if (history != null)
            {
                foreach (var item in history)
                {
                    if (item == null || string.IsNullOrWhiteSpace(item.role) || string.IsNullOrWhiteSpace(item.content))
                    {
                        continue;
                    }

                    messages.Add(new MoonshotMessage
                    {
                        role = item.role,
                        content = item.content
                    });
                }
            }

            messages.Add(new MoonshotMessage
            {
                role = "user",
                content = message
            });

            var request = new MoonshotChatRequest
            {
                model = string.IsNullOrWhiteSpace(config.moonshotModel) ? "moonshot-v1-8k" : config.moonshotModel,
                messages = messages.ToArray(),
                temperature = 0.85f,
                max_tokens = 180
            };

            var baseUrl = string.IsNullOrWhiteSpace(config.moonshotBaseUrl) ? "https://api.moonshot.cn/v1" : config.moonshotBaseUrl.TrimEnd('/');
            var url = $"{baseUrl}/chat/completions";
            var json = JsonUtility.ToJson(request);
            using (var webRequest = new UnityWebRequest(url, "POST"))
            {
                webRequest.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
                webRequest.downloadHandler = new DownloadHandlerBuffer();
                webRequest.timeout = 60;
                webRequest.SetRequestHeader("Authorization", $"Bearer {config.moonshotApiKey}");
                webRequest.SetRequestHeader("Content-Type", "application/json");

                yield return webRequest.SendWebRequest();

                if (webRequest.result != UnityWebRequest.Result.Success)
                {
                    onError?.Invoke($"{webRequest.error}\nURL: {url}\nHTTP: {webRequest.responseCode}\n{webRequest.downloadHandler.text}");
                    yield break;
                }

                var response = JsonUtility.FromJson<MoonshotChatResponse>(webRequest.downloadHandler.text);
                var reply = response?.choices != null && response.choices.Length > 0
                    ? response.choices[0]?.message?.content
                    : null;

                if (string.IsNullOrWhiteSpace(reply))
                {
                    onError?.Invoke($"Kimi 返回为空。\nURL: {url}\n{webRequest.downloadHandler.text}");
                    yield break;
                }

                onSuccess?.Invoke(new ChatResponse
                {
                    animalId = animalId,
                    reply = reply,
                    suggestedQuestions = new[] { "你平时吃什么？", "你为什么会濒危？", "我可以怎样保护你？" },
                    missionHint = "可以去完成“帮森森找到食物”任务。"
                });
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

    [Serializable]
    public class MoonshotChatRequest
    {
        public string model;
        public MoonshotMessage[] messages;
        public float temperature;
        public int max_tokens;
    }

    [Serializable]
    public class MoonshotMessage
    {
        public string role;
        public string content;
    }

    [Serializable]
    public class MoonshotChatResponse
    {
        public MoonshotChoice[] choices;
    }

    [Serializable]
    public class MoonshotChoice
    {
        public MoonshotMessage message;
    }
}
