namespace EndangeredAR.AI
{
    public enum AIFinalSource
    {
        OnDeviceLlm,
        DevelopmentRemoteLlm,
        MemoryDeterministic,
        LocalLlm,
        CloudLlm,
        ServerRule,
        ServerKnowledge,
        UnityFallback,
        SystemStatus
    }

    public static class AIFinalSourceProtocol
    {
        public static bool TryParseExact(string wireValue, out AIFinalSource source)
        {
            switch (wireValue)
            {
                case "on_device_llm":
                    source = AIFinalSource.OnDeviceLlm;
                    return true;
                case "development_remote_llm":
                    source = AIFinalSource.DevelopmentRemoteLlm;
                    return true;
                case "memory_deterministic":
                    source = AIFinalSource.MemoryDeterministic;
                    return true;
                case "local_llm":
                    source = AIFinalSource.LocalLlm;
                    return true;
                case "cloud_llm":
                    source = AIFinalSource.CloudLlm;
                    return true;
                case "server_rule":
                    source = AIFinalSource.ServerRule;
                    return true;
                case "server_knowledge":
                    source = AIFinalSource.ServerKnowledge;
                    return true;
                case "unity_fallback":
                    source = AIFinalSource.UnityFallback;
                    return true;
                case "system_status":
                    source = AIFinalSource.SystemStatus;
                    return true;
                default:
                    source = default;
                    return false;
            }
        }

        public static string ToWireValue(AIFinalSource source)
        {
            switch (source)
            {
                case AIFinalSource.OnDeviceLlm:
                    return "on_device_llm";
                case AIFinalSource.DevelopmentRemoteLlm:
                    return "development_remote_llm";
                case AIFinalSource.MemoryDeterministic:
                    return "memory_deterministic";
                case AIFinalSource.LocalLlm:
                    return "local_llm";
                case AIFinalSource.CloudLlm:
                    return "cloud_llm";
                case AIFinalSource.ServerRule:
                    return "server_rule";
                case AIFinalSource.ServerKnowledge:
                    return "server_knowledge";
                case AIFinalSource.UnityFallback:
                    return "unity_fallback";
                case AIFinalSource.SystemStatus:
                    return "system_status";
                default:
                    return string.Empty;
            }
        }
    }

    internal static class AIProvenanceProtocol
    {
        public static string[] ParseProviderAttempt(string wireValue)
        {
            switch (wireValue)
            {
                case "on_device_llm":
                case "development_remote_llm":
                case "local_llm":
                case "cloud_llm":
                case "unity_fallback":
                    return new[] { wireValue };
                case "none":
                case null:
                case "":
                    return System.Array.Empty<string>();
                default:
                    return System.Array.Empty<string>();
            }
        }

        public static string SanitizeReasonCode(string value)
        {
            if (string.IsNullOrEmpty(value) || value.Length > 64)
            {
                return string.Empty;
            }

            foreach (var character in value)
            {
                if (!(character == '_' || (character >= 'a' && character <= 'z') ||
                      (character >= '0' && character <= '9')))
                {
                    return string.Empty;
                }
            }

            return value;
        }
    }
}
