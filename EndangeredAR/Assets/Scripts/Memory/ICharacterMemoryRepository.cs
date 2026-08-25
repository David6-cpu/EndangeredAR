namespace EndangeredAR.Memory
{
    public interface ICharacterMemoryRepository
    {
        CharacterMemoryLoadResult Load();
        void Save(CharacterMemoryDocument document);
    }
}
