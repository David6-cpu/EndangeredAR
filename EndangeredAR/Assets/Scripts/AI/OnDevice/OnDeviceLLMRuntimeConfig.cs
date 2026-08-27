using System;

namespace EndangeredAR.AI.OnDevice
{
    public sealed class OnDeviceLLMRuntimeConfig
    {
        public OnDeviceLLMRuntimeConfig(
            string profileName,
            int contextSize,
            int threadCount,
            int batchSize,
            int microBatchSize)
        {
            if (string.IsNullOrWhiteSpace(profileName) || contextSize < 256 || threadCount < 1 ||
                batchSize < 1 || microBatchSize < 1 || microBatchSize > batchSize)
            {
                throw new ArgumentOutOfRangeException(nameof(contextSize));
            }

            ProfileName = profileName;
            ContextSize = contextSize;
            ThreadCount = threadCount;
            BatchSize = batchSize;
            MicroBatchSize = microBatchSize;
        }

        public string ProfileName { get; }
        public int ContextSize { get; }
        public int ThreadCount { get; }
        public int BatchSize { get; }
        public int MicroBatchSize { get; }

        public static OnDeviceLLMRuntimeConfig FirstProductionProfile =>
            new OnDeviceLLMRuntimeConfig("qwen15b_ios_ctx2048_v1", 2048, 4, 256, 256);
    }
}
