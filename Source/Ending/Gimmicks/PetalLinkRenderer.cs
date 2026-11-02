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

        // 셰이더 흐름 좌표의 float 정밀도를 유지하도록 1배속 기준 1시간마다 시간을 되감는다.
        private const int AgeWrapTicks = 216000;

        // width는 꽃잎 나선이 차지하는 폭(칸)이다. seed는 연결선마다 꽃잎 배치가 겹치지 않게 하는 값이다.
        public static void Draw(Vector3 a, Vector3 b, float width, Color color, int seed)
        {
            var delta = b - a;
            var length = delta.MagnitudeHorizontal();
            if (length < 0.05f || width <= 0f || color.a <= 0f) { return; }

            // SoulLinkRenderer와 같이 메시의 로컬 X축(셰이더 U)을 a→b 방향에 맞춘다. 꽃잎은 a에서 b로 흐른다.
            var angle = Mathf.Atan2(-delta.z, delta.x) * 57.29578f;
            var center = (a + b) * 0.5f;
            center.y = AltitudeLayer.MoteOverhead.AltitudeFor();

            var matrix = Matrix4x4.TRS(center, Quaternion.AngleAxis(angle, Vector3.up), new Vector3(length, 1f, width));

            PropertyBlock.Clear();
            PropertyBlock.SetFloat(ShaderPropertyIDs.AgeSecs, (Find.TickManager.TicksGame % AgeWrapTicks) / 60f);
            PropertyBlock.SetColor(ShaderPropertyIDs.Color, color);
            PropertyBlock.SetFloat(LengthPropertyId, length);
            PropertyBlock.SetFloat(WidthPropertyId, width);
            PropertyBlock.SetFloat(SeedPropertyId, seed & 1023);

            Graphics.DrawMesh(MeshPool.plane10, matrix, LinkMat, 0, null, 0, PropertyBlock);
        }
    }
}
