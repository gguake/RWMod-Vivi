using LudeonTK;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using Verse;

namespace VVRace
{
    public struct ManaUpdateJob : IJobParallelForBatch
    {
        [ReadOnly]
        public NativeArray<float> manaReserveGrid;

        [ReadOnly]
        public NativeArray<float> manaGrid;

        [WriteOnly]
        public NativeArray<float> outputGrid;

        public void Execute(int startIndex, int count)
        {
            int idx = 0;
            while (idx < count)
            {
                if (idx + 4 < count)
                {
                    var r = new float4(manaReserveGrid[idx], manaReserveGrid[idx + 1], manaReserveGrid[idx + 2], manaReserveGrid[idx + 3]) + new float4(manaGrid[idx], manaGrid[idx + 1], manaGrid[idx + 2], manaGrid[idx + 3]);
                    outputGrid[idx] = math.clamp(r.x, 0f, EnvironmentManaGrid.EnvironmentManaMax);
                    outputGrid[idx + 1] = math.clamp(r.y, 0f, EnvironmentManaGrid.EnvironmentManaMax);
                    outputGrid[idx + 2] = math.clamp(r.z, 0f, EnvironmentManaGrid.EnvironmentManaMax);
                    outputGrid[idx + 3] = math.clamp(r.w, 0f, EnvironmentManaGrid.EnvironmentManaMax);
                    idx += 4;
                }
                else
                {
                    for (; idx < count; ++idx)
                    {
                        outputGrid[idx] = Mathf.Clamp(manaReserveGrid[idx] + manaGrid[idx], 0f, EnvironmentManaGrid.EnvironmentManaMax);
                    }
                }
            }
        }
    }
}
