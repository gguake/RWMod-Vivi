using RimWorld;
using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace VVRace
{
    // 형체를 빚는 연출이 공유하는 재질과 그리기. 위치와 진행도는 각 연출에서 계산한다.
    [StaticConstructorOnStartup]
    internal static class ManifestationRenderer
    {
        private static readonly int ProgressId = Shader.PropertyToID("_Progress");
        private static readonly int RevealId = Shader.PropertyToID("_Reveal");
        private static readonly MaterialPropertyBlock PropertyBlock = new MaterialPropertyBlock();
        private static readonly Dictionary<string, Material> MaterialCache = new Dictionary<string, Material>();

        public static void Draw(string texPath, Rot4 rotation, Vector3 pos, float scale, float ageSecs, float progress, float reveal)
        {
            var suffix = rotation == Rot4.North ? "_north" : (rotation == Rot4.South ? "_south" : "_east");
            var material = GetMaterial(texPath + suffix);
            if (material == null) { return; }

            PropertyBlock.Clear();
            PropertyBlock.SetFloat(ShaderPropertyIDs.AgeSecs, ageSecs);
            PropertyBlock.SetFloat(ProgressId, progress);
            PropertyBlock.SetFloat(RevealId, reveal);

            var mesh = rotation == Rot4.West ? MeshPool.plane10Flip : MeshPool.plane10;
            var matrix = Matrix4x4.TRS(pos, Quaternion.identity, new Vector3(scale, 1f, scale));
            Graphics.DrawMesh(mesh, matrix, material, 0, null, 0, PropertyBlock);
        }

        private static Material GetMaterial(string fullPath)
        {
            if (MaterialCache.TryGetValue(fullPath, out var cached))
            {
                return cached;
            }

            var texture = ContentFinder<Texture2D>.Get(fullPath, reportFailure: false);
            var noise = ContentFinder<Texture2D>.Get("VFX/VV_DissolveNoise", reportFailure: false);
            Material material = null;
            if (texture != null)
            {
                material = MaterialPool.MatFrom(texture, ShaderDatabase.LoadShader("Map/VV_Manifest"), Color.white);
                if (noise != null)
                {
                    material.SetTexture("_DissolveTex", noise);
                }
            }

            MaterialCache[fullPath] = material;
            return material;
        }
    }
}
