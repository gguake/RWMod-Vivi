using RimWorld;
using UnityEngine;
using Verse;

namespace VVRace
{
    [StaticConstructorOnStartup]
    public static class AsphodelBondChainRenderer
    {
        private const string ShaderPath = "Map/VV_AsphodelBondChain";
        private const string ChainTexPath = "VFX/VV_BondChainStrip";
        private const float AltitudeBelowPawn = 0.14f;
        private const float QuadMargin = 0.6f;
        private const float MinDrawRadius = 0.08f;

        private static readonly Color ChainColor = new Color(0.95f, 0.98f, 1f, 1f);
        private static readonly Material ChainMat;
        private static readonly MaterialPropertyBlock PropertyBlock = new MaterialPropertyBlock();

        private static readonly int RadiusXId = Shader.PropertyToID("_RadiusX");
        private static readonly int RadiusZId = Shader.PropertyToID("_RadiusZ");
        private static readonly int HalfXId = Shader.PropertyToID("_HalfX");
        private static readonly int HalfZId = Shader.PropertyToID("_HalfZ");
        private static readonly int ProgressId = Shader.PropertyToID("_Progress");

        static AsphodelBondChainRenderer()
        {
            var shader = ShaderDatabase.LoadShader(ShaderPath);
            if (shader != null)
            {
                var tex = ContentFinder<Texture2D>.Get(ChainTexPath, reportFailure: false) ?? BaseContent.WhiteTex;
                ChainMat = MaterialPool.MatFrom(tex, shader, Color.white);
            }
        }

        public static void Draw(Pawn target, float progress)
        {
            if (ChainMat == null || target == null || !target.Spawned) { return; }

            var clampedProgress = Mathf.Clamp01(progress);
            var shrink = Mathf.Clamp01(1f - clampedProgress);
            var radiusX = FairyToil_AsphodelBondOrbit.StartRadiusX * shrink;
            var radiusZ = FairyToil_AsphodelBondOrbit.StartRadiusZ * shrink;
            if (radiusX < MinDrawRadius || radiusZ < MinDrawRadius) { return; }

            var halfX = radiusX + QuadMargin;
            var halfZ = radiusZ + QuadMargin;
            var targetDraw = target.DrawPos;
            var center = new Vector3(
                targetDraw.x,
                targetDraw.y - AltitudeBelowPawn,
                targetDraw.z + FairyToil_AsphodelBondOrbit.LiftStart * shrink);

            PropertyBlock.Clear();
            PropertyBlock.SetColor(ShaderPropertyIDs.Color, ChainColor);
            PropertyBlock.SetFloat(ShaderPropertyIDs.AgeSecs, LinkRenderUtility.WrappedAgeSecs);
            PropertyBlock.SetFloat(RadiusXId, radiusX);
            PropertyBlock.SetFloat(RadiusZId, radiusZ);
            PropertyBlock.SetFloat(HalfXId, halfX);
            PropertyBlock.SetFloat(HalfZId, halfZ);
            PropertyBlock.SetFloat(ProgressId, clampedProgress);

            Graphics.DrawMesh(
                MeshPool.plane10,
                Matrix4x4.TRS(center, Quaternion.identity, new Vector3(halfX * 2f, 1f, halfZ * 2f)),
                ChainMat,
                0,
                null,
                0,
                PropertyBlock);
        }
    }
}
