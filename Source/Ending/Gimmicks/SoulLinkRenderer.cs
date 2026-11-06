using RimWorld;
using UnityEngine;
using Verse;

namespace VVRace
{
    [StaticConstructorOnStartup]
    internal static class SoulLinkRenderer
    {
        private static readonly Material BeamMat = MaterialPool.MatFrom("VFX/VV_SoulLinkBeam", ShaderDatabase.LoadShader("Map/VV_SoulLink"), Color.white);
        private static readonly MaterialPropertyBlock PropertyBlock = new MaterialPropertyBlock();
        private static readonly int TilingPropertyId = Shader.PropertyToID("_Tiling");

        // 텍스처 한 장이 덮는 길이(칸). 빔이 길어지면 텍스처를 늘리지 않고 반복한다.
        private const float TileLength = 4f;

        public static void Draw(Vector3 a, Vector3 b, float width, Color color)
        {
            if (!LinkRenderUtility.TryGetMatrix(a, b, width, color, out var matrix, out var length)) { return; }

            PropertyBlock.Clear();
            PropertyBlock.SetFloat(ShaderPropertyIDs.AgeSecs, LinkRenderUtility.WrappedAgeSecs);
            PropertyBlock.SetColor(ShaderPropertyIDs.Color, color);
            PropertyBlock.SetFloat(TilingPropertyId, Mathf.Max(1f, length / TileLength));

            Graphics.DrawMesh(MeshPool.plane10, matrix, BeamMat, 0, null, 0, PropertyBlock);
        }
    }
}
