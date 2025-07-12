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
        public int w;

        [ReadOnly]
        public int h;

        [ReadOnly]
        public NativeArray<float> manaGrid;

        [WriteOnly]
        public NativeArray<float> outputGrid;

        [ReadOnly]
        public bool checkFlowerCells;

        [WriteOnly]
        public NativePriorityQueue<int, float, FloatMinComparer> flowerCellQueue;

        public void Execute(int i)
        {
            var x = i % w;
            var y = i / h;

            var left = x > 0;
            var up = y > 0;
            var right = x < w - 1;
            var down = y < h - 1;

            var v1 = new float4(
                left && up ? manaGrid[i - 1 - w] : 0f,
                right && up ? manaGrid[i + 1 - w] : 0f,
                left && down ? manaGrid[i - 1 + w] : 0f,
                right && down ? manaGrid[i + 1 + w] : 0f);

            var v2 = new float4(
                left ? manaGrid[i - 1] : 0f,
                up ? manaGrid[i - w] : 0f,
                right ? manaGrid[i + 1] : 0f,
                down ? manaGrid[i + w] : 0f);

            var r = (math.csum(v1) + math.csum(v2) * 2f + manaGrid[i] * 4f) / 16f;
            outputGrid[i] = r > 0.01f ? r > 1000f ? 1000f : r : 0f;

            if (checkFlowerCells && r >= 50f)
            {
                flowerCellQueue.Enqueue(i, -math.clamp(r, 50f, 500f));
            }
        }
    }
}
