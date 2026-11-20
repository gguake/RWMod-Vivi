using UnityEngine;
using Verse;

namespace VVRace
{
    [StaticConstructorOnStartup]
    public static class RuinDistortionRenderer
    {
        private const string ShaderPath = "Map/VV_RuinDistortion";
        private const string BaseTexPath = "Things/Mote/Black";
        private const float QuadWidth = 52f;
        private const float QuadHeight = 96f;
        private const float QuadCenterOffsetZ = 5.5f;

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
            }
        }

        public static void Draw(IntVec3 origin, Vector4 sealState, float distortionStrength = 13.5f)
        {
            if (DistortionMat == null) { return; }
            if (sealState.x <= 0.001f && sealState.y <= 0.001f && sealState.z <= 0.001f && sealState.w <= 0.001f) { return; }

            var centerX = origin.x + 0.5f;
            var centerZ = origin.z + 0.5f + QuadCenterOffsetZ;
            var drawPos = new Vector3(centerX, AltitudeLayer.MoteOverhead.AltitudeFor() + 0.03f, centerZ);

            // 셰이더 내부 rel은 origin 중심 기준이므로, 쿼드 중심 이동량만큼 보정하지 않도록 origin 중심 큰 대칭 쿼드를 쓴다.
            var symSizeX = 52f;
            var symSizeZ = 104f;
            var symPos = new Vector3(origin.x + 0.5f, drawPos.y, origin.z + 0.5f);

            PropertyBlock.Clear();
            PropertyBlock.SetFloat(ShaderPropertyIDs.AgeSecs, LinkRenderUtility.WrappedAgeSecs);
            PropertyBlock.SetVector(BoundsXZId, new Vector4(symPos.x, symPos.z, symSizeX, symSizeZ));
            PropertyBlock.SetVector(SealStateId, sealState);
            PropertyBlock.SetFloat(DistortionId, distortionStrength);

            var matrix = Matrix4x4.TRS(symPos, Quaternion.identity, new Vector3(symSizeX, 1f, symSizeZ));
            Graphics.DrawMesh(MeshPool.plane10, matrix, DistortionMat, 0, null, 0, PropertyBlock);
        }
    }
}
