using RimWorld;
using UnityEngine;
using Verse;

namespace VVRace
{
    // VV_SoulLink 셰이더(가산 혼합, U = 빔 길이 방향)로 두 점 사이에 영혼 연결 빔을 그린다.
    // 머티리얼은 시작 시 한 번만 만들고, 빔마다 다른 값(색·타일링·시간)은 MaterialPropertyBlock으로 넘긴다.
    [StaticConstructorOnStartup]
    internal static class SoulLinkRenderer
    {
        private static readonly Material BeamMat = MaterialPool.MatFrom("VFX/VV_SoulLinkBeam", ShaderDatabase.LoadShader("Map/VV_SoulLink"), Color.white);
        private static readonly MaterialPropertyBlock PropertyBlock = new MaterialPropertyBlock();
        private static readonly int TilingPropertyId = Shader.PropertyToID("_Tiling");

        // 빔 텍스처 한 장이 덮는 길이(칸). 길이가 길어지면 늘어나는 대신 반복된다.
        private const float TileLength = 4f;

        // 게임 시간이 매우 커지면 셰이더 안 스크롤 좌표의 float 정밀도가 떨어지므로 1시간(실시간 1배속 기준)마다 되감는다.
        private const int AgeWrapTicks = 216000;

        public static void Draw(Vector3 a, Vector3 b, float width, Color color)
        {
            var delta = b - a;
            var length = delta.MagnitudeHorizontal();
            if (length < 0.05f || width <= 0f || color.a <= 0f) { return; }

            // 메시 로컬 X축(텍스처 U)을 a→b 방향으로 돌린다. (FleckMaker.ConnectingLine과 같은 규칙)
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
