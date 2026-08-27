using System;

namespace EndangeredAR.AI.OnDevice
{
    public interface IOnDeviceLLMNativeBackend : IDisposable
    {
        bool IsSupported { get; }
        OnDeviceLLMNativeState State { get; }
        bool StartLoad(string modelPath, int contextSize, int threadCount);
        bool StartGenerate(string prompt, int maxTokens);
        string ReadOutput();
        string ReadError();
        OnDeviceLLMMetrics ReadMetrics();
        void Cancel();
    }
}
