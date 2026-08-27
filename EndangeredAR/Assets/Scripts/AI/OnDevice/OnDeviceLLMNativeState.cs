namespace EndangeredAR.AI.OnDevice
{
    public enum OnDeviceLLMNativeState
    {
        Unsupported = 0,
        Uninitialized = 1,
        Loading = 2,
        Ready = 3,
        Generating = 4,
        Completed = 5,
        Cancelled = 6,
        Error = 7
    }
}
