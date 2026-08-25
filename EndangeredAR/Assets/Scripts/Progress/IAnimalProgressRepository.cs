namespace EndangeredAR.Progress
{
    public interface IAnimalProgressRepository
    {
        AnimalProgressDocument Load();
        void Save(AnimalProgressDocument document);
    }
}
