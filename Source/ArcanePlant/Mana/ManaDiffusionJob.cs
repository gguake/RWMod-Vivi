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

            var result = 0f;
            if (flowFromLeft == 0 && flowFromRight == 0 && flowFromUp == 0 && flowFromDown == 0)
            {
                outputGrid[index] = 0;
                return;
            }

            var horizontal = flowFromLeft > 0f && flowFromRight > 0f;
            var vertical = flowFromUp > 0f && flowFromDown > 0f;
            if (!(horizontal ^ vertical))
            {
                result = manaGrid[index] * 0.35f + (flowFromLeft + flowFromRight + flowFromUp + flowFromDown) * 0.162f;
            }
            else
            {
                result = manaGrid[index] * 0.65f + (flowFromLeft + flowFromRight + flowFromUp + flowFromDown) * 0.087f;
            }

            if (result < 0.1f)
            {
                result = 0f;
            }

            outputGrid[index] = result;
        }
    }
}
