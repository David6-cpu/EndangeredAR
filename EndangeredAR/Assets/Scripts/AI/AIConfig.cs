using UnityEngine.Serialization;
using UnityEngine;

namespace EndangeredAR.AI
{
    public enum AIProviderMode
    {
        OnDevice,
        DevelopmentRemote,
        DevelopmentCloud
    }

    [CreateAssetMenu(menuName = "Endangered AR/AI Config")]
    public sealed class AIConfig : ScriptableObject
    {
        public AIProviderMode providerMode = AIProviderMode.OnDevice;
        public AIRouteMode routeMode = AIRouteMode.LocalOnly;
        [FormerlySerializedAs("localServerUrl")]
        public string developmentRemoteServerUrl = string.Empty;
        public float localTimeoutSeconds = 8f;
        public float totalTimeoutSeconds = 38f;
    }
}
