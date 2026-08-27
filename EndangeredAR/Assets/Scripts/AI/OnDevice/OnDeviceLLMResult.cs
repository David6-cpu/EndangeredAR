namespace EndangeredAR.AI.OnDevice
{
    public sealed class OnDeviceLLMResult
    {
        public OnDeviceLLMResult(string generationId, string text, OnDeviceLLMMetrics metrics)
        {
            GenerationId = generationId ?? string.Empty;
            Text = text ?? string.Empty;
            Metrics = metrics ?? OnDeviceLLMMetrics.Empty;
        }

        public string GenerationId { get; }
        public string Text { get; }
        public OnDeviceLLMMetrics Metrics { get; }
    }

    public sealed class OnDeviceLLMError
    {
        public OnDeviceLLMError(string code, string message, bool isTimeout)
        {
            Code = code ?? "on_device_native_error";
            Message = message ?? "On-device generation failed.";
            IsTimeout = isTimeout;
        }

        public string Code { get; }
        public string Message { get; }
        public bool IsTimeout { get; }
    }
}
