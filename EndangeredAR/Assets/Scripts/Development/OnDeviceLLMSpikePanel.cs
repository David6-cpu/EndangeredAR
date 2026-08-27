#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.IO;
using EndangeredAR.AI.OnDevice;
using UnityEngine;

namespace EndangeredAR.Development
{
    public sealed class OnDeviceLLMSpikePanel : MonoBehaviour
    {
        private const int ContextSize = 2048;
        private const int MaxTokens = 64;
        private const float PanelWidth = 460f;
        private const float PanelHeight = 420f;
        private static OnDeviceLLMSpikePanel instance;

        private OnDeviceLLMSpikeRunner runner;
        private Vector2 scroll;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void EnsureInstance()
        {
            if (instance != null)
            {
                return;
            }

            var host = new GameObject("OnDevice LLM Spike");
            DontDestroyOnLoad(host);
            instance = host.AddComponent<OnDeviceLLMSpikePanel>();
        }

        private void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }

            instance = this;
            var threads = Mathf.Clamp(SystemInfo.processorCount - 2, 1, 6);
            runner = new OnDeviceLLMSpikeRunner(
                new OnDeviceLLMNativeBackend(),
                ContextSize,
                threads,
                MaxTokens);
        }

        private void Update()
        {
            runner?.Poll();
        }

        private void OnApplicationPause(bool paused)
        {
            runner?.OnApplicationPause(paused);
        }

        private void OnDestroy()
        {
            runner?.Dispose();
            runner = null;
            if (instance == this)
            {
                instance = null;
            }
        }

        private void OnGUI()
        {
            if (runner == null)
            {
                return;
            }

            var x = Mathf.Max(16f, Screen.width - PanelWidth - 16f);
            GUILayout.BeginArea(new Rect(x, 210f, PanelWidth, PanelHeight), GUI.skin.box);
            GUILayout.Label("ON-DEVICE QWEN SPIKE");
            GUILayout.Label("Prompt: " + OnDeviceLLMSpikeRunner.FixedPrompt);
            GUILayout.Label("Status: " + runner.Status);
            GUILayout.Label("Generator: " + (string.IsNullOrEmpty(runner.Generator) ? "none" : runner.Generator));

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("LOAD QWEN", GUILayout.Height(44f)))
            {
                runner.StartLoad(ResolveBundledModelPath());
            }

            if (GUILayout.Button("RUN FIXED PROMPT", GUILayout.Height(44f)))
            {
                runner.StartFixedPrompt();
            }

            if (GUILayout.Button("CANCEL", GUILayout.Height(44f)))
            {
                runner.Cancel();
            }
            GUILayout.EndHorizontal();

            var metrics = runner.Metrics ?? OnDeviceLLMMetrics.Empty;
            GUILayout.Label("Load ms: " + metrics.modelLoadMs);
            GUILayout.Label("First token ms: " + metrics.firstTokenMs);
            GUILayout.Label("Total ms: " + metrics.totalMs);
            GUILayout.Label("Tokens/s: " + metrics.tokensPerSecond.ToString("F2"));
            GUILayout.Label("Peak memory MB: " + (metrics.peakMemoryBytes / (1024f * 1024f)).ToString("F1"));
            GUILayout.Label("Thermal: " + metrics.thermalBefore + " -> " + metrics.thermalAfter);
            GUILayout.Label("Metal: " + (metrics.metalEnabled ? "enabled" : "not_confirmed"));

            if (!string.IsNullOrEmpty(runner.Error))
            {
                GUILayout.Label("Error: " + runner.Error);
            }

            scroll = GUILayout.BeginScrollView(scroll, GUILayout.Height(100f));
            GUILayout.Label(string.IsNullOrEmpty(runner.Output) ? "No native completion yet." : runner.Output);
            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        private static string ResolveBundledModelPath()
        {
            return Path.Combine(
                Application.streamingAssetsPath,
                "OnDeviceModels",
                OnDeviceLLMSpikeRunner.ModelFileName);
        }
    }
}
#endif
