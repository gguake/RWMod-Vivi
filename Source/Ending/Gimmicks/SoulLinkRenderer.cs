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

        // 셰이더 스크롤 좌표의 float 정밀도를 유지하도록 1배속 기준 1시간마다 시간을 되감는다.
        private const int AgeWrapTicks = 216000;

        public static void Draw(Vector3 a, Vector3 b, float width, Color color)
        {
            var delta = b - a;
            var length = delta.MagnitudeHorizontal();
            if (length < 0.05f || width <= 0f || color.a <= 0f) { return; }

            // FleckMaker.ConnectingLine과 같이 메시의 로컬 X축(텍스처 U)을 a→b 방향에 맞춘다.
            var angle = Mathf.Atan2(-delta.z, delta.x) * 57.29578f;
            var center = (a + b) * 0.5f;
            center.y = AltitudeLayer.MoteOverhead.AltitudeFor();

            var matrix = Matrix4x4.TRS(center, Quaternion.AngleAxis(angle, Vector3.up), new Vector3(length, 1f, width));

            PropertyBlock.Clear();
            PropertyBlock.SetFloat(ShaderPropertyIDs.AgeSecs, (Find.TickManager.TicksGame % AgeWrapTicks) / 60f);
            PropertyBlock.SetColor(ShaderPropertyIDs.Color, color);
            PropertyBlock.SetFloat(TilingPropertyId, Mathf.Max(1f, length / TileLength));

            Graphics.DrawMesh(MeshPool.plane10, matrix, BeamMat, 0, null, 0, PropertyBlock);
        }
    }
}
