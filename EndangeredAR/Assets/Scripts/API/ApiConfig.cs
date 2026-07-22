using UnityEngine;

namespace EndangeredAR.API
{
    [CreateAssetMenu(menuName = "Endangered AR/API Config")]
    public class ApiConfig : ScriptableObject
    {
        [Header("Server Proxy")]
        public string baseUrl = "http://127.0.0.1:8000";
    }
}
