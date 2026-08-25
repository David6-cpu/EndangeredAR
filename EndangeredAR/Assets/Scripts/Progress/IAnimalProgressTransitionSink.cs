namespace EndangeredAR.Progress
{
    public interface IAnimalProgressTransitionSink
    {
        void AppendBatch(AnimalProgressTransitionBatch batch);
    }
}
