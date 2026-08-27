using System;

namespace EndangeredAR.AI.OnDevice
{
    [Serializable]
    public sealed class OnDeviceLLMMetrics
    {
        public long modelLoadMs;
        public long firstTokenMs;
        public long totalMs;
        public int generatedTokens;
        public float tokensPerSecond;
        public long peakMemoryBytes;
        public string thermalBefore = "unknown";
        public string thermalAfter = "unknown";
        public bool metalEnabled;

        public static OnDeviceLLMMetrics Empty => new OnDeviceLLMMetrics();
    }
}
