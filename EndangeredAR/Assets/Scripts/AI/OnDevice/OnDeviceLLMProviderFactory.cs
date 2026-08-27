using System;
using System.IO;
using UnityEngine;

namespace EndangeredAR.AI.OnDevice
{
    public static class OnDeviceLLMProviderFactory
    {
        public const string ModelFileName = "qwen2.5-1.5b-instruct-q4_k_m.gguf";
        private const string ModelDirectoryName = "OnDeviceModels";

        public static IOnDeviceLLMProvider CreateProduction()
        {
            return new OnDeviceLLMProvider(
                new OnDeviceLLMNativeBackend(),
                ResolveBundledModelPath(Application.streamingAssetsPath),
                OnDeviceLLMRuntimeConfig.FirstProductionProfile);
        }

        public static string ResolveBundledModelPath(string streamingAssetsPath)
        {
            if (string.IsNullOrWhiteSpace(streamingAssetsPath))
            {
                throw new ArgumentException("StreamingAssets path is unavailable.", nameof(streamingAssetsPath));
            }

            return Path.Combine(streamingAssetsPath, ModelDirectoryName, ModelFileName);
        }
    }
}
