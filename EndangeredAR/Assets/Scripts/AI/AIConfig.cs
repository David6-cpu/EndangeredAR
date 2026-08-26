using UnityEngine;

namespace EndangeredAR.AI
{
    [CreateAssetMenu(menuName = "Endangered AR/AI Config")]
    public sealed class AIConfig : ScriptableObject
    {
        public AIRouteMode routeMode = AIRouteMode.LocalOnly;
        public string localServerUrl = "http://127.0.0.1:8000";
        public float localTimeoutSeconds = 8f;
        public float totalTimeoutSeconds = 38f;
    }
}
