using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Verse;

namespace VVRace
{
    [BurstCompile]
    public struct ManaDiffusionJob : IJobParallelFor
    {
        [ReadOnly]
        public IntVec2 mapSize;

        [ReadOnly]
        public NativeArray<float> manaGrid;

        [WriteOnly]
        public NativeArray<float> outputGrid;

        [BurstCompile]
        public void Execute(int index)
        {
            var x = index % mapSize.x;
            var y = index / mapSize.z;

            var flowFromLeft = x > 0 ? manaGrid[index - 1] : 0f;
            var flowFromRight = x < mapSize.x - 1 ? manaGrid[index + 1] : 0f;
            var flowFromUp = y > 0 ? manaGrid[index - mapSize.x] : 0f;
            var flowFromDown = y < mapSize.z - 1 ? manaGrid[index + mapSize.x] : 0f;

            var result = manaGrid[index] * 0.5f + (flowFromLeft + flowFromRight + flowFromUp + flowFromDown) * 0.125f;

            var horizontal = flowFromLeft > 0f && flowFromRight > 0f;
            var vertical = flowFromUp > 0f && flowFromDown > 0f;
            if (!(horizontal ^ vertical))
            {
                result *= 0.9f;
            }

            if (result < 0.1f)
            {
                result = 0f;
            }

            outputGrid[index] = result;
        }
    }
}
