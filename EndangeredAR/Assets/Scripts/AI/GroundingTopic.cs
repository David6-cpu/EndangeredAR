namespace EndangeredAR.AI
{
    public enum GroundingTopic
    {
        None,
        Diet
    }

    internal static class GroundingTopicProtocol
    {
        public static GroundingTopic Parse(string rawValue)
        {
            return rawValue == "diet" ? GroundingTopic.Diet : GroundingTopic.None;
        }
    }
}
