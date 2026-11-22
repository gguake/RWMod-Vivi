using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using Verse;

namespace VVRace
{
    public class DissolveMask
    {
        public const float EdgeWidth = 0.07f;

        private const int Grid = 64;
        private const float MinAlpha = 0.5f;
        private const string NoiseTexPath = "VFX/VV_DissolveNoise";

        private static readonly Dictionary<string, DissolveMask> Cache = new Dictionary<string, DissolveMask>();
        private static float[] _noise;
        private static int _noiseSize;

        private readonly Vector2[] _uvs;
        private readonly Color[] _colors;
        private readonly float[] _noiseValues;
        private readonly float[] _keys;
        private readonly int[] _order;
        private Vector2 _keyDir = new Vector2(float.NaN, float.NaN);
        private float _keyWeight = float.NaN;

        public int Count => _uvs.Length;

        public static float CellUV => 1f / Grid;

        private DissolveMask(List<Vector2> uvs, List<Color> colors, List<float> noise)
        {
            _uvs = uvs.ToArray();
            _colors = colors.ToArray();
            _noiseValues = noise.ToArray();
            _keys = new float[_uvs.Length];
            _order = new int[_uvs.Length];
        }

        public static DissolveMask Get(string texPath)
        {
            if (texPath.NullOrEmpty()) { return null; }
            if (Cache.TryGetValue(texPath, out var cached)) { return cached; }

            DissolveMask mask = null;
            try
            {
                mask = Load(texPath);
            }
            catch (Exception e)
            {
                Log.Warning($"[VVRace] dissolve mask load failed for {texPath}: {e.Message}");
            }

            Cache[texPath] = mask;
            return mask;
        }

        public static Vector3 ToWorld(Vector2 uv, Vector3 center, Vector2 size, bool flip)
        {
            var x = (uv.x - 0.5f) * size.x;
            return new Vector3(center.x + (flip ? -x : x), center.y, center.z + (uv.y - 0.5f) * size.y);
        }

        public static float Threshold(float progress)
        {
            return progress * (1f + EdgeWidth);
        }

        public bool TryGetKeyRange(Vector2 sweepDir, float sweepWeight, out float min, out float max)
        {
            EnsureKeys(sweepDir, sweepWeight);
            min = _keys.Length > 0 ? _keys[0] : 0f;
            max = _keys.Length > 0 ? _keys[_keys.Length - 1] : 1f;
            return _keys.Length > 0;
        }

        public int CountBetween(float lo, float hi, Vector2 sweepDir, float sweepWeight)
        {
            EnsureKeys(sweepDir, sweepWeight);
            return Mathf.Max(0, LowerBound(hi) - LowerBound(lo));
        }

        public bool TrySample(float lo, float hi, Vector2 sweepDir, float sweepWeight, out Vector2 uv, out Color color)
        {
            EnsureKeys(sweepDir, sweepWeight);
            var from = LowerBound(lo);
            var to = LowerBound(hi);
            if (to <= from)
            {
                uv = default;
                color = default;
                return false;
            }

            var index = _order[Rand.Range(from, to)];
            uv = _uvs[index] + new Vector2(Rand.Range(-0.5f, 0.5f), Rand.Range(-0.5f, 0.5f)) * CellUV;
            color = _colors[index];
            return true;
        }

        public bool TrySampleRemaining(float threshold, Vector2 sweepDir, float sweepWeight, out Vector2 uv, out Color color)
        {
            return TrySample(threshold, float.MaxValue, sweepDir, sweepWeight, out uv, out color);
        }

        private void EnsureKeys(Vector2 sweepDir, float sweepWeight)
        {
            if (sweepDir == _keyDir && sweepWeight == _keyWeight) { return; }

            _keyDir = sweepDir;
            _keyWeight = sweepWeight;
            var dir = sweepDir.sqrMagnitude > 1e-8f ? sweepDir.normalized : Vector2.right;
            for (int i = 0; i < _uvs.Length; i++)
            {
                var sweep = Mathf.Clamp01(0.5f - Vector2.Dot(_uvs[i] - new Vector2(0.5f, 0.5f), dir));
                var n = _noiseValues[i];
                _keys[i] = Mathf.Lerp(n, sweep * 0.8f + n * 0.2f, sweepWeight);
                _order[i] = i;
            }

            Array.Sort(_order, (a, b) => _keys[a].CompareTo(_keys[b]));
            var sorted = new float[_keys.Length];
            for (int i = 0; i < _order.Length; i++)
            {
                sorted[i] = _keys[_order[i]];
            }
            Array.Copy(sorted, _keys, sorted.Length);
        }

        private int LowerBound(float value)
        {
            int lo = 0, hi = _keys.Length;
            while (lo < hi)
            {
                var mid = (lo + hi) >> 1;
                if (_keys[mid] < value)
                {
                    lo = mid + 1;
                }
                else
                {
                    hi = mid;
                }
            }
            return lo;
        }

        private static DissolveMask Load(string texPath)
        {
            if (!TryReadPixels(texPath, out var pixels, out var width, out var height)) { return null; }
            if (!EnsureNoise()) { return null; }

            var uvs = new List<Vector2>();
            var colors = new List<Color>();
            var noise = new List<float>();
            for (int gy = 0; gy < Grid; gy++)
            {
                for (int gx = 0; gx < Grid; gx++)
                {
                    var uv = new Vector2((gx + 0.5f) / Grid, (gy + 0.5f) / Grid);
                    var px = pixels[Mathf.Min(height - 1, (int)(uv.y * height)) * width + Mathf.Min(width - 1, (int)(uv.x * width))];
                    if (px.a < MinAlpha * 255f) { continue; }

                    uvs.Add(uv);
                    colors.Add(px);
                    noise.Add(_noise[Mathf.Min(_noiseSize - 1, (int)(uv.y * _noiseSize)) * _noiseSize + Mathf.Min(_noiseSize - 1, (int)(uv.x * _noiseSize))]);
                }
            }

            return uvs.Count > 0 ? new DissolveMask(uvs, colors, noise) : null;
        }

        private static bool EnsureNoise()
        {
            if (_noise != null) { return true; }
            if (!TryReadPixels(NoiseTexPath, out var pixels, out var width, out var height) || width != height) { return false; }

            _noiseSize = width;
            _noise = new float[pixels.Length];
            for (int i = 0; i < pixels.Length; i++)
            {
                _noise[i] = pixels[i].r / 255f;
            }
            return true;
        }

        private static bool TryReadPixels(string texPath, out Color32[] pixels, out int width, out int height)
        {
            pixels = null;
            width = height = 0;
            var content = VVThingDefOf.VV_DyingApocrypha?.modContentPack;
            if (content == null) { return false; }

            foreach (var folder in content.foldersToLoadDescendingOrder)
            {
                var file = Path.Combine(folder, "Textures", texPath + ".png");
                if (!File.Exists(file)) { continue; }

                var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                try
                {
                    if (!texture.LoadImage(File.ReadAllBytes(file))) { return false; }

                    pixels = texture.GetPixels32();
                    width = texture.width;
                    height = texture.height;
                    return pixels != null && pixels.Length == width * height;
                }
                finally
                {
                    UnityEngine.Object.Destroy(texture);
                }
            }

            return false;
        }
    }
}
