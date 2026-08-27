using System;
using System.Collections;

namespace EndangeredAR.AI.OnDevice
{
    public interface IOnDeviceTokenCounter
    {
        int CountTokens(System.Collections.Generic.IReadOnlyList<OnDeviceChatMessage> messages);
    }

    public interface IOnDeviceLLMProvider : IDisposable, IOnDeviceTokenCounter
    {
        string GeneratorId { get; }
        IEnumerator Send(
            OnDeviceLLMRequest request,
            float timeoutSeconds,
            Action<OnDeviceLLMResult> onSuccess,
            Action<OnDeviceLLMError> onError);
        void Cancel(string generationId);
        void OnApplicationPause(bool paused);
    }
}
