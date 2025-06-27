using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

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

            var r = math.csum(k / 16f * (v1 + 2 * v2 + v3));
            outputGrid[idx] = math.clamp(r, 0f, EnvironmentManaGrid.EnvironmentManaMax);
        }
    }
}
