using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace VVRace
{
    public readonly struct DissolveStyle
    {
        public readonly float delaySecs;
        public readonly float durationSecs;
        public readonly Color edgeColor;
        public readonly float sweepWeight;
        public readonly float ashAmount;

        public DissolveStyle(float delaySecs, float durationSecs, Color edgeColor, float sweepWeight, float ashAmount)
        {
            this.delaySecs = delaySecs;
            this.durationSecs = durationSecs;
            this.edgeColor = edgeColor;
            this.sweepWeight = sweepWeight;
            this.ashAmount = ashAmount;
        }
    }

    [StaticConstructorOnStartup]
    public static class DissolveRenderer
    {
        private const string ShaderPath = "Map/VV_Dissolve";
        private const string NoiseTexPath = "VFX/VV_DissolveNoise";

        private static readonly int SweepDirId = Shader.PropertyToID("_SweepDir");
        private static readonly int SweepWeightId = Shader.PropertyToID("_SweepWeight");
        private static readonly int AshAmountId = Shader.PropertyToID("_AshAmount");
        private static readonly int EdgeColorId = Shader.PropertyToID("_EdgeColor");
        private static readonly int DissolveDelayId = Shader.PropertyToID("_DissolveDelay");
        private static readonly int DissolveDurationId = Shader.PropertyToID("_DissolveDuration");
        private static readonly MaterialPropertyBlock PropertyBlock = new MaterialPropertyBlock();
        private static readonly Dictionary<string, Material> MaterialCache = new Dictionary<string, Material>();

        public static Material GetMaterial(string texPath)
        {
            return GetNoiseMaterial(texPath, ShaderPath, MaterialCache, noiseFirst: false);
        }

        public static Material GetMaterial(string texPath, Rot4 rotation)
        {
            return GetMaterial(texPath + RotationSuffix(rotation));
        }

        internal static string RotationSuffix(Rot4 rotation)
        {
            return rotation == Rot4.North ? "_north" : (rotation == Rot4.South ? "_south" : "_east");
        }

        internal static Material GetNoiseMaterial(string texPath, string shaderPath, Dictionary<string, Material> cache, bool noiseFirst)
        {
            if (cache.TryGetValue(texPath, out var cached))
            {
                return cached;
            }

            var texture = ContentFinder<Texture2D>.Get(texPath, reportFailure: false);
            var noise = noiseFirst ? ContentFinder<Texture2D>.Get(NoiseTexPath, reportFailure: false) : null;
            Material material = null;
            if (texture != null)
            {
                material = MaterialPool.MatFrom(texture, ShaderDatabase.LoadShader(shaderPath), Color.white);

                if (!noiseFirst)
                {
                    noise = ContentFinder<Texture2D>.Get(NoiseTexPath, reportFailure: false);
                }
                if (noise != null)
                {
                    material.SetTexture("_DissolveTex", noise);
                }
            }

            cache[texPath] = material;
            return material;
        }

        public static void Draw(Material material, Vector3 pos, Vector2 size, bool flip, float ageSecs, in DissolveStyle style, Vector2 sweepDir)
        {
            if (material == null) { return; }

            PropertyBlock.Clear();
            PropertyBlock.SetFloat(ShaderPropertyIDs.AgeSecs, ageSecs);
            PropertyBlock.SetFloat(DissolveDelayId, style.delaySecs);
            PropertyBlock.SetFloat(DissolveDurationId, style.durationSecs);
            PropertyBlock.SetColor(EdgeColorId, style.edgeColor);
            PropertyBlock.SetVector(SweepDirId, new Vector4(flip ? -sweepDir.x : sweepDir.x, sweepDir.y, 0f, 0f));
            PropertyBlock.SetFloat(SweepWeightId, style.sweepWeight);
            PropertyBlock.SetFloat(AshAmountId, style.ashAmount);

            var mesh = flip ? MeshPool.plane10Flip : MeshPool.plane10;
            var matrix = Matrix4x4.TRS(pos, Quaternion.identity, new Vector3(size.x, 1f, size.y));
            Graphics.DrawMesh(mesh, matrix, material, 0, null, 0, PropertyBlock);
        }
    }
}
