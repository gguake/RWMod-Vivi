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
