namespace EndangeredAR.Memory
{
    public enum CharacterMemoryStoreStatus
    {
        Available,
        RecoveredFromBackup,
        RecoveredEmpty,
        FutureVersion,
        Unavailable
    }

    public sealed class CharacterMemoryLoadResult
    {
        public CharacterMemoryLoadResult(
            CharacterMemoryDocument document,
            CharacterMemoryStoreStatus status,
            bool canWrite)
        {
            Document = document;
            Status = status;
            CanWrite = canWrite;
        }

        public CharacterMemoryDocument Document { get; }
        public CharacterMemoryStoreStatus Status { get; }
        public bool CanWrite { get; }
    }
}
