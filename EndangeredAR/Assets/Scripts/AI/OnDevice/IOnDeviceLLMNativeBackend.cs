using System;

namespace EndangeredAR.AI.OnDevice
{
    public interface IOnDeviceLLMNativeBackend : IDisposable
    {
        bool IsSupported { get; }
        OnDeviceLLMNativeState State { get; }
        bool StartLoad(
            string modelPath,
            int contextSize,
            int threadCount,
            int batchSize,
            int microBatchSize);
        int CountTokens(string messagesJson);
        bool StartGenerate(
            string messagesJson,
            int maxTokens,
            float temperature,
            float topP,
            float repeatPenalty,
            uint seed);
        string ReadOutput();
        string ReadError();
        OnDeviceLLMMetrics ReadMetrics();
        void Cancel();
    }
}
