using LudeonTK;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using Verse;

namespace VVRace
{
    public struct ManaDiffusionJob : IJobParallelFor
    {
        [ReadOnly]
        public int width;

        [ReadOnly]
        public int height;

        [ReadOnly]
        public NativeArray<float> manaGrid;

        [WriteOnly]
        public NativeArray<float> outputGrid;

        [ReadOnly]
        public bool checkFlowerCells;

        [WriteOnly]
        public NativePriorityQueue<int, float, FloatMinComparer> flowerCellQueue;

        public void Execute(int idx)
        {
            var x = idx % width;
            var y = idx / height;

            var k = new float3(1, 2, 1);
            var v1 = y > 0 ? 
                new float3(x > 0 ? manaGrid[idx - width - 1] : 0f, manaGrid[idx - width], x < width - 1 ? manaGrid[idx - width + 1] : 0f) :
                float3.zero;

            var v2 = new float3(x > 0 ? manaGrid[idx - 1] : 0f, manaGrid[idx], x < width - 1 ? manaGrid[idx + 1] : 0f);

            var v3 = y < height - 1 ?
                new float3(x > 0 ? manaGrid[idx + width - 1] : 0f, manaGrid[idx + width], x < width - 1 ? manaGrid[idx + width + 1] : 0f) :
                float3.zero;

            var r = math.clamp(math.csum(k * (v1 + 2 * v2 + v3)) / 16f, 0f, EnvironmentManaGrid.EnvironmentManaMax);
            outputGrid[idx] = r > 0.01f ? r : 0f;

            if (checkFlowerCells && r >= 150f)
            {
                flowerCellQueue.Enqueue(idx, -r);
            }
        }
    }
}
