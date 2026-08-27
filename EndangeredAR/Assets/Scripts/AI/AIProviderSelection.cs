namespace EndangeredAR.AI
{
    public static class AIProviderSelection
    {
        public static AIProviderMode Resolve(
            AIProviderMode configured,
            bool developmentRoutesAllowed)
        {
            if (!developmentRoutesAllowed)
            {
                return AIProviderMode.OnDevice;
            }

            switch (configured)
            {
                case AIProviderMode.DevelopmentRemote:
                case AIProviderMode.DevelopmentCloud:
                    return configured;
                case AIProviderMode.OnDevice:
                default:
                    return AIProviderMode.OnDevice;
            }
        }
    }
}
