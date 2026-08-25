using System.Collections.Generic;

namespace EndangeredAR.Memory
{
    public interface ICharacterMemoryProgressSnapshotSource
    {
        IReadOnlyList<CharacterMemoryProgressSnapshot> GetSnapshots();
    }
}
