using RimWorld;
using UnityEngine;
using Verse;

namespace VVRace
{
    [StaticConstructorOnStartup]
    internal static class PetalLinkRenderer
    {
        private static readonly Material LinkMat = MaterialPool.MatFrom("VFX/VV_EverflowerPetal", ShaderDatabase.LoadShader("Map/VV_PetalLink"), Color.white);
        private static readonly MaterialPropertyBlock PropertyBlock = new MaterialPropertyBlock();
        private static readonly int LengthPropertyId = Shader.PropertyToID("_Length");
        private static readonly int WidthPropertyId = Shader.PropertyToID("_Width");
        private static readonly int SeedPropertyId = Shader.PropertyToID("_Seed");

        public static void Draw(Vector3 a, Vector3 b, float width, Color color, int seed)
        {
            if (!LinkRenderUtility.TryGetMatrix(a, b, width, color, out var matrix, out var length)) { return; }

            PropertyBlock.Clear();
            PropertyBlock.SetFloat(ShaderPropertyIDs.AgeSecs, LinkRenderUtility.WrappedAgeSecs);
            PropertyBlock.SetColor(ShaderPropertyIDs.Color, color);
            PropertyBlock.SetFloat(LengthPropertyId, length);
            PropertyBlock.SetFloat(WidthPropertyId, width);
            PropertyBlock.SetFloat(SeedPropertyId, seed & 1023);

            Graphics.DrawMesh(MeshPool.plane10, matrix, LinkMat, 0, null, 0, PropertyBlock);
        }
    }
}
