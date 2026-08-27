using System;
using System.Runtime.InteropServices;
using System.Text;
using UnityEngine;

namespace EndangeredAR.AI.OnDevice
{
    public sealed class OnDeviceLLMNativeBackend : IOnDeviceLLMNativeBackend
    {
        private const int TextBufferCapacity = 16384;
        private const string UnsupportedError = "on_device_llm_unsupported";

        public bool IsSupported
        {
            get
            {
#if UNITY_IOS && !UNITY_EDITOR
                return true;
#else
                return false;
#endif
            }
        }

        public OnDeviceLLMNativeState State
        {
            get
            {
#if UNITY_IOS && !UNITY_EDITOR
                var value = NativeGetState();
                switch (value)
                {
                    case 0:
                        return OnDeviceLLMNativeState.Uninitialized;
                    case 1:
                        return OnDeviceLLMNativeState.Loading;
                    case 2:
                        return OnDeviceLLMNativeState.Ready;
                    case 3:
                        return OnDeviceLLMNativeState.Generating;
                    case 4:
                        return OnDeviceLLMNativeState.Completed;
                    case 5:
                        return OnDeviceLLMNativeState.Cancelled;
                    case 6:
                        return OnDeviceLLMNativeState.Error;
                    default:
                        return OnDeviceLLMNativeState.Error;
                }
#else
                return OnDeviceLLMNativeState.Unsupported;
#endif
            }
        }

        public bool StartLoad(string modelPath, int contextSize, int threadCount)
        {
#if UNITY_IOS && !UNITY_EDITOR
            if (string.IsNullOrWhiteSpace(modelPath) || contextSize < 256 || threadCount < 1)
            {
                return false;
            }

            return NativeStartLoad(modelPath, contextSize, threadCount) == 1;
#else
            return false;
#endif
        }

        public bool StartGenerate(string prompt, int maxTokens)
        {
#if UNITY_IOS && !UNITY_EDITOR
            if (string.IsNullOrWhiteSpace(prompt) || maxTokens < 1 || maxTokens > 256)
            {
                return false;
            }

            return NativeStartGenerate(prompt, maxTokens) == 1;
#else
            return false;
#endif
        }

        public string ReadOutput()
        {
#if UNITY_IOS && !UNITY_EDITOR
            return ReadNativeString(NativeCopyOutput);
#else
            return string.Empty;
#endif
        }

        public string ReadError()
        {
#if UNITY_IOS && !UNITY_EDITOR
            return ReadNativeString(NativeCopyError);
#else
            return UnsupportedError;
#endif
        }

        public OnDeviceLLMMetrics ReadMetrics()
        {
#if UNITY_IOS && !UNITY_EDITOR
            var json = ReadNativeString(NativeCopyMetricsJson);
            if (string.IsNullOrEmpty(json))
            {
                return OnDeviceLLMMetrics.Empty;
            }

            try
            {
                return JsonUtility.FromJson<OnDeviceLLMMetrics>(json) ?? OnDeviceLLMMetrics.Empty;
            }
            catch (ArgumentException)
            {
                return OnDeviceLLMMetrics.Empty;
            }
#else
            return OnDeviceLLMMetrics.Empty;
#endif
        }

        public void Cancel()
        {
#if UNITY_IOS && !UNITY_EDITOR
            NativeCancel();
#endif
        }

        public void Dispose()
        {
#if UNITY_IOS && !UNITY_EDITOR
            NativeRelease();
#endif
        }

#if UNITY_IOS && !UNITY_EDITOR
        private delegate int NativeCopy(byte[] buffer, int capacity);

        private static string ReadNativeString(NativeCopy copy)
        {
            var buffer = new byte[TextBufferCapacity];
            var length = copy(buffer, buffer.Length);
            if (length <= 0)
            {
                return string.Empty;
            }

            var safeLength = Math.Min(length, buffer.Length - 1);
            return Encoding.UTF8.GetString(buffer, 0, safeLength);
        }

        [DllImport("__Internal", EntryPoint = "endar_llm_start_load", CallingConvention = CallingConvention.Cdecl)]
        private static extern int NativeStartLoad(
            [MarshalAs(UnmanagedType.LPUTF8Str)] string modelPath,
            int contextSize,
            int threadCount);

        [DllImport("__Internal", EntryPoint = "endar_llm_start_generate", CallingConvention = CallingConvention.Cdecl)]
        private static extern int NativeStartGenerate(
            [MarshalAs(UnmanagedType.LPUTF8Str)] string prompt,
            int maxTokens);

        [DllImport("__Internal", EntryPoint = "endar_llm_get_state", CallingConvention = CallingConvention.Cdecl)]
        private static extern int NativeGetState();

        [DllImport("__Internal", EntryPoint = "endar_llm_copy_output", CallingConvention = CallingConvention.Cdecl)]
        private static extern int NativeCopyOutput([Out] byte[] buffer, int capacity);

        [DllImport("__Internal", EntryPoint = "endar_llm_copy_error", CallingConvention = CallingConvention.Cdecl)]
        private static extern int NativeCopyError([Out] byte[] buffer, int capacity);

        [DllImport("__Internal", EntryPoint = "endar_llm_copy_metrics_json", CallingConvention = CallingConvention.Cdecl)]
        private static extern int NativeCopyMetricsJson([Out] byte[] buffer, int capacity);

        [DllImport("__Internal", EntryPoint = "endar_llm_cancel", CallingConvention = CallingConvention.Cdecl)]
        private static extern void NativeCancel();

        [DllImport("__Internal", EntryPoint = "endar_llm_release", CallingConvention = CallingConvention.Cdecl)]
        private static extern void NativeRelease();
#endif
    }
}
