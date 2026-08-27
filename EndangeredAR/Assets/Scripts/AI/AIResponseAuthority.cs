namespace EndangeredAR.AI
{
    public enum ContentAuthority
    {
        None,
        CanonicalKnowledge,
        CurrentProgress,
        CharacterMemory,
        SystemPolicy
    }

    public static class ContentAuthorityProtocol
    {
        public static bool TryParseExact(string wireValue, out ContentAuthority authority)
        {
            switch (wireValue)
            {
                case "none":
                    authority = ContentAuthority.None;
                    return true;
                case "canonical_knowledge":
                    authority = ContentAuthority.CanonicalKnowledge;
                    return true;
                case "current_progress":
                    authority = ContentAuthority.CurrentProgress;
                    return true;
                case "character_memory":
                    authority = ContentAuthority.CharacterMemory;
                    return true;
                case "system_policy":
                    authority = ContentAuthority.SystemPolicy;
                    return true;
                default:
                    authority = default;
                    return false;
            }
        }

        public static string ToWireValue(ContentAuthority authority)
        {
            switch (authority)
            {
                case ContentAuthority.None:
                    return "none";
                case ContentAuthority.CanonicalKnowledge:
                    return "canonical_knowledge";
                case ContentAuthority.CurrentProgress:
                    return "current_progress";
                case ContentAuthority.CharacterMemory:
                    return "character_memory";
                case ContentAuthority.SystemPolicy:
                    return "system_policy";
                default:
                    return "none";
            }
        }
    }

    public enum LanguageGenerator
    {
        None,
        OnDeviceLlm,
        DevelopmentRemoteLlm,
        LocalLlm,
        CloudLlm
    }

    public static class LanguageGeneratorProtocol
    {
        public static bool TryParseExact(string wireValue, out LanguageGenerator generator)
        {
            switch (wireValue)
            {
                case "none":
                    generator = LanguageGenerator.None;
                    return true;
                case "on_device_llm":
                    generator = LanguageGenerator.OnDeviceLlm;
                    return true;
                case "development_remote_llm":
                    generator = LanguageGenerator.DevelopmentRemoteLlm;
                    return true;
                case "local_llm":
                    generator = LanguageGenerator.LocalLlm;
                    return true;
                case "cloud_llm":
                    generator = LanguageGenerator.CloudLlm;
                    return true;
                default:
                    generator = default;
                    return false;
            }
        }

        public static string ToWireValue(LanguageGenerator generator)
        {
            switch (generator)
            {
                case LanguageGenerator.None:
                    return "none";
                case LanguageGenerator.OnDeviceLlm:
                    return "on_device_llm";
                case LanguageGenerator.DevelopmentRemoteLlm:
                    return "development_remote_llm";
                case LanguageGenerator.LocalLlm:
                    return "local_llm";
                case LanguageGenerator.CloudLlm:
                    return "cloud_llm";
                default:
                    return "none";
            }
        }
    }
}
