using System.Numerics;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Waylith.Jobs;

namespace VVRace
{
    public struct ManaDiffusionJob : IWaylithParallelForJob
    {
        [ReadOnly]
        public int w;

        [ReadOnly]
        public int h;

        [ReadOnly]
        public NativeArray<float> manaGrid;

        [WriteOnly]
        public NativeArray<float> outputGrid;

        // Each invocation owns one output row; the input grid is never modified.
        public unsafe void Execute(int y)
        {
            var input = (float*)manaGrid.GetUnsafeReadOnlyPtr();
            var output = (float*)outputGrid.GetUnsafePtr();
            var rowStart = y * w;

            if (y == 0 || y == h - 1 || w <= 2)
            {
                for (int x = 0; x < w; ++x)
                {
                    DiffuseCell(rowStart + x, x, y, input, output);
                }
                return;
            }

            DiffuseCell(rowStart, 0, y, input, output);

            var column = 1;
            if (Vector.IsHardwareAccelerated)
            {
                var threshold = new Vector<float>(0.01f);
                var maximum = new Vector<float>(1000f);
                // Include the right neighbor of the last lane without crossing a row.
                var lastVectorColumn = w - 1 - Vector<float>.Count;
                for (; column <= lastVectorColumn; column += Vector<float>.Count)
                {
                    var i = rowStart + column;
                    var diagonal =
                        (Vector.LoadUnsafe(ref input[i - 1 - w]) + Vector.LoadUnsafe(ref input[i + 1 - w])) +
                        (Vector.LoadUnsafe(ref input[i - 1 + w]) + Vector.LoadUnsafe(ref input[i + 1 + w]));
                    var adjacent =
                        (Vector.LoadUnsafe(ref input[i - 1]) + Vector.LoadUnsafe(ref input[i - w])) +
                        (Vector.LoadUnsafe(ref input[i + 1]) + Vector.LoadUnsafe(ref input[i + w]));

                    var r = (diagonal * 5f + adjacent * 8f + Vector.LoadUnsafe(ref input[i]) * 11.8f) / 64f;
                    Vector.ConditionalSelect(Vector.GreaterThan(r, threshold), Vector.Min(r, maximum), Vector<float>.Zero)
                        .StoreUnsafe(ref output[i]);
                }
            }

            for (; column < w; ++column)
            {
                DiffuseCell(rowStart + column, column, y, input, output);
            }
        }

        private unsafe void DiffuseCell(int i, int x, int y, float* input, float* output)
        {
            var left = x > 0;
            var up = y > 0;
            var right = x < w - 1;
            var down = y < h - 1;

            var upperLeft = left && up ? input[i - 1 - w] : 0f;
            var upperRight = right && up ? input[i + 1 - w] : 0f;
            var lowerLeft = left && down ? input[i - 1 + w] : 0f;
            var lowerRight = right && down ? input[i + 1 + w] : 0f;

            var leftValue = left ? input[i - 1] : 0f;
            var upperValue = up ? input[i - w] : 0f;
            var rightValue = right ? input[i + 1] : 0f;
            var lowerValue = down ? input[i + w] : 0f;

            var r = (((upperLeft + upperRight) + (lowerLeft + lowerRight)) * 5f +
                ((leftValue + upperValue) + (rightValue + lowerValue)) * 8f + input[i] * 11.8f) / 64f;
            output[i] = r > 0.01f ? r > 1000f ? 1000f : r : 0f;
        }
    }
}
