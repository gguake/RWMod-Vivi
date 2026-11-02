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

    // VV_Dissolve 소멸 연출이 공유하는 재질과 그리기.
    // 연출별 값은 그릴 때 MaterialPropertyBlock으로 넣으므로 공유 재질에는 노이즈 텍스처만 지정한다.
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
            if (MaterialCache.TryGetValue(texPath, out var cached))
            {
                return cached;
            }

            var texture = ContentFinder<Texture2D>.Get(texPath, reportFailure: false);
            Material material = null;
            if (texture != null)
            {
                material = MaterialPool.MatFrom(texture, ShaderDatabase.LoadShader(ShaderPath), Color.white);

                var noise = ContentFinder<Texture2D>.Get(NoiseTexPath, reportFailure: false);
                if (noise != null)
                {
                    material.SetTexture("_DissolveTex", noise);
                }
            }

            MaterialCache[texPath] = material;
            return material;
        }

        // Graphic_Multi처럼 서쪽은 동쪽 텍스처를 좌우 반전해 쓴다.
        public static Material GetMaterial(string texPath, Rot4 rotation)
        {
            var suffix = rotation == Rot4.North ? "_north" : (rotation == Rot4.South ? "_south" : "_east");
            return GetMaterial(texPath + suffix);
        }

        // sweepDir는 텍스처 UV 기준 방향이다. 좌우 반전하면 메시의 UV도 반전되므로 x를 뒤집는다.
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
