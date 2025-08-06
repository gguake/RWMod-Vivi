using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace VVRace
{
    public struct ManaUpdateJob : IJobParallelFor
    {
        [ReadOnly]
        public NativeArray<float> manaReserveGrid;

        [ReadOnly]
        public NativeArray<float> manaGrid;

        [WriteOnly]
        public NativeArray<float> outputGrid;

        public void Execute(int index)
        {
            outputGrid[index] = math.clamp(manaGrid[index] + manaReserveGrid[index], 0f, ManaMapComponent.EnvironmentManaMax);
        }
    }
}
