using UnityEngine;
using Verse;

namespace VVRace
{
    internal static class LinkRenderUtility
    {
        // 셰이더 흐름 좌표의 float 정밀도를 유지하도록 1배속 기준 1시간마다 시간을 되감는다.
        private const int AgeWrapTicks = 216000;

        public static float WrappedAgeSecs => (Find.TickManager.TicksGame % AgeWrapTicks) / 60f;

        public static bool TryGetMatrix(Vector3 a, Vector3 b, float width, Color color, out Matrix4x4 matrix, out float length)
        {
            var delta = b - a;
            length = delta.MagnitudeHorizontal();
            matrix = default;
            if (length < 0.05f || width <= 0f || color.a <= 0f) { return false; }

            // FleckMaker.ConnectingLine처럼 메시의 로컬 X축(텍스처 U)을 a→b 방향에 맞춘다.
            var angle = Mathf.Atan2(-delta.z, delta.x) * 57.29578f;
            var center = (a + b) * 0.5f;
            center.y = AltitudeLayer.MoteOverhead.AltitudeFor();
            matrix = Matrix4x4.TRS(center, Quaternion.AngleAxis(angle, Vector3.up), new Vector3(length, 1f, width));
            return true;
        }
    }
}
