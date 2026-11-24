using UnityEngine;
using Verse;

namespace VVRace
{
    [StaticConstructorOnStartup]
    public static class RuinDistortionRenderer
    {
        private const string ShaderPath = "Map/VV_RuinDistortion";
        private const string BaseTexPath = "VFX/VV_EverflowerPetal";
        private const string NoiseTexPath = "VFX/VV_TileableFbmNoise";
        private const string PollenTexPath = "VFX/VV_RuinPollen";
        private const string LotusTexPath = "VFX/VV_RuinLotusSdf";
        private const float SymSizeX = 156f;
        private const float SymSizeZ = 140f;

        private static readonly Material DistortionMat;
        private static readonly MaterialPropertyBlock PropertyBlock = new MaterialPropertyBlock();

        private static readonly int BoundsXZId = Shader.PropertyToID("_BoundsXZ");
        private static readonly int SealStateId = Shader.PropertyToID("_SealState");
        private static readonly int DistortionId = Shader.PropertyToID("_Distortion");

        static RuinDistortionRenderer()
        {
            var shader = ShaderDatabase.LoadShader(ShaderPath);
            var tex = ContentFinder<Texture2D>.Get(BaseTexPath, reportFailure: false) ?? BaseContent.BlackTex;
            if (shader != null && tex != null)
            {
                DistortionMat = MaterialPool.MatFrom(tex, shader, Color.white);
                var noise = ContentFinder<Texture2D>.Get(NoiseTexPath, reportFailure: false);
                if (noise != null)
                {
                    DistortionMat.SetTexture("_NoiseTex", noise);
                }
                var pollen = ContentFinder<Texture2D>.Get(PollenTexPath, reportFailure: false);
                if (pollen != null)
                {
                    DistortionMat.SetTexture("_PollenTex", pollen);
                }
                var lotus = ContentFinder<Texture2D>.Get(LotusTexPath, reportFailure: false);
                if (lotus != null)
                {
                    DistortionMat.SetTexture("_LotusTex", lotus);
                }
            }
        }

        public static void Draw(IntVec3 origin, Vector4 sealState, float distortionStrength = 13.5f, int rotation = 0)
        {
            if (DistortionMat == null) { return; }
            if (sealState.x <= 0.001f && sealState.y <= 0.001f && sealState.z <= 0.001f && sealState.w <= 0.001f) { return; }

            var symPos = new Vector3(origin.x + 0.5f, AltitudeLayer.MoteOverhead.AltitudeFor() + 0.03f, origin.z + 0.5f);

            PropertyBlock.Clear();
            PropertyBlock.SetFloat(ShaderPropertyIDs.AgeSecs, LinkRenderUtility.WrappedAgeSecs);
            PropertyBlock.SetVector(BoundsXZId, new Vector4(symPos.x, symPos.z, SymSizeX, SymSizeZ));
            PropertyBlock.SetVector(SealStateId, sealState);
            PropertyBlock.SetFloat(DistortionId, distortionStrength);

            var rotQuat = Quaternion.Euler(0f, (rotation & 3) * 90f, 0f);
            var matrix = Matrix4x4.TRS(symPos, rotQuat, new Vector3(SymSizeX, 1f, SymSizeZ));
            Graphics.DrawMesh(MeshPool.plane10, matrix, DistortionMat, 0, null, 0, PropertyBlock);
        }
    }
}
