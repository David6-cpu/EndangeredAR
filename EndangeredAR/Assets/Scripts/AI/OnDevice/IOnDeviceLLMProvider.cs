using System;
using System.Collections;

namespace EndangeredAR.AI.OnDevice
{
    public interface IOnDeviceLLMProvider : IDisposable
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
