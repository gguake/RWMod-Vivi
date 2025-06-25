using Unity.Collections;
using Unity.Jobs;
using Verse;

namespace VVRace
{
    public struct ManaDiffusionJob : IJobParallelFor
    {
        private const float DistributeRatio = 0.16f;

        [ReadOnly]
        public IntVec2 mapSize;

        [ReadOnly]
        public NativeArray<float> manaGrid;

        [WriteOnly]
        public NativeArray<float> outputGrid;

        public void Execute(int index)
        {
            var x = index % mapSize.x;
            var y = index / mapSize.z;

            var cell = manaGrid[index];
            var leftCell = x > 0 ? manaGrid[index - 1] : 0f;
            var rightCell = x < mapSize.x - 1 ? manaGrid[index + 1] : 0f;
            var upCell = y > 0 ? manaGrid[index - mapSize.x] : 0f;
            var downCell = y < mapSize.z - 1 ? manaGrid[index + mapSize.x] : 0f;

            if (leftCell == 0 && rightCell == 0 && upCell == 0 && downCell == 0)
            {
                outputGrid[index] = cell * (1f - DistributeRatio * 4f);
                return;
            }

            float result = cell * (1 - DistributeRatio * 4f);
            result += leftCell > cell ? (leftCell + cell) * DistributeRatio : cell * DistributeRatio;
            result += rightCell > cell ? (rightCell + cell) * DistributeRatio : cell * DistributeRatio;
            result += upCell > cell ? (upCell + cell) * DistributeRatio : cell * DistributeRatio;
            result += downCell > cell ? (downCell + cell) * DistributeRatio : cell * DistributeRatio;

            result *= 0.75f;
            if (result < 0.1f)
            {
                result = 0f;
            }

            outputGrid[index] = result;
        }
    }
}
