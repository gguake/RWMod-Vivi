using UnityEngine;
using Verse;

namespace VVRace
{
    internal static class FairyJobUtility
    {
        public const float IdleOrbitRadiusX = 0.6f;
        public const float IdleOrbitRadiusZ = 0.3f;
        public const float IdleOrbitDepth = 0.06f;
        public const float IdleOrbitAngularSpeed = 0.008f;
        public const int TargetScanIntervalTicks = 6;

        public static Vector3 OrbitPositionAround(ViviFairy fairy, Pawn centerPawn, int slot, int count)
        {
            if (!TryGetOrbitParameters(fairy, centerPawn, slot, count, out var center, out var angle, out var radiusX, out var radiusZ, out var tiltCos, out var tiltSin))
            {
                return FallbackOrbitPosition(fairy);
            }

            return center + OrbitOffset(angle, radiusX, radiusZ, tiltCos, tiltSin);
        }

        public static void IdleOrbitAround(ViviFairy fairy, Pawn centerPawn, int slot, int count)
        {
            if (fairy == null || fairy.Destroyed || fairy.State != FairyState.Idle || fairy.Map == null) { return; }

            if (!TryGetOrbitParameters(fairy, centerPawn, slot, count, out var center, out var angle, out var radiusX, out var radiusZ, out var tiltCos, out var tiltSin))
            {
                return;
            }

            var orbit = center + OrbitOffset(angle, radiusX, radiusZ, tiltCos, tiltSin);
            var tangent = OrbitTangent(angle, radiusX, radiusZ, tiltCos, tiltSin) * fairy.OrbitDirection;
            var centerDraw = centerPawn.DrawPos;
            float depthRadius = OrbitDepthRadius(radiusX, radiusZ, tiltCos, tiltSin);
            float rel = Mathf.Clamp((centerDraw.z - orbit.z) / depthRadius, -1f, 1f);
            float drawAltitude = centerDraw.y + Mathf.Lerp(-IdleOrbitDepth, IdleOrbitDepth, 0.5f + 0.5f * rel);

            fairy.SetToilPosition(orbit, tangent, drawAltitude);
        }

        public static Vector3 IdleOrbitPositionAround(ViviFairy fairy, Pawn centerPawn, int slot, int count)
        {
            return OrbitPositionAround(fairy, centerPawn, slot, count);
        }

        private static bool TryGetOrbitParameters(
            ViviFairy fairy,
            Pawn centerPawn,
            int slot,
            int count,
            out Vector3 center,
            out float angle,
            out float radiusX,
            out float radiusZ,
            out float tiltCos,
            out float tiltSin)
        {
            center = Vector3.zero;
            angle = 0f;
            radiusX = 0f;
            radiusZ = 0f;
            tiltCos = 1f;
            tiltSin = 0f;

            if (fairy == null || fairy.Controller?.Props == null || centerPawn == null || !centerPawn.Spawned || centerPawn.Map != fairy.Map)
            {
                return false;
            }

            float slotOffset = slot * (Mathf.PI * 2f / Mathf.Max(1, count));
            // 틱 누적값이 커지면 float 유효자릿수가 틱당 회전량보다 커져 궤도가 끊기므로 double로 계산 후 2π로 접는다.
            double motion = GenTicks.TicksGame * (double)IdleOrbitAngularSpeed * fairy.OrbitSpeedFactor * fairy.OrbitDirection;
            angle = (float)((motion + slotOffset + fairy.OrbitPhaseOffset) % (System.Math.PI * 2.0));
            radiusX = IdleOrbitRadiusX * fairy.OrbitRadiusXFactor;
            radiusZ = IdleOrbitRadiusZ * fairy.OrbitRadiusZFactor;
            tiltCos = Mathf.Cos(fairy.OrbitTiltAngle);
            tiltSin = Mathf.Sin(fairy.OrbitTiltAngle);
            center = centerPawn.DrawPos.Yto0();
            return true;
        }

        private static Vector3 OrbitOffset(float angle, float radiusX, float radiusZ, float tiltCos, float tiltSin)
        {
            float cos = Mathf.Cos(angle);
            float sin = Mathf.Sin(angle);
            var vector = new Vector3(cos * radiusX, 0f, sin * radiusZ);
            return new Vector3(
                vector.x * tiltCos - vector.z * tiltSin,
                0f,
                vector.x * tiltSin + vector.z * tiltCos);
        }

        private static Vector3 OrbitTangent(float angle, float radiusX, float radiusZ, float tiltCos, float tiltSin)
        {
            float cos = Mathf.Cos(angle);
            float sin = Mathf.Sin(angle);
            var tangent = new Vector3(-sin * radiusX, 0f, cos * radiusZ);
            return new Vector3(
                tangent.x * tiltCos - tangent.z * tiltSin,
                0f,
                tangent.x * tiltSin + tangent.z * tiltCos);
        }

        private static float OrbitDepthRadius(float radiusX, float radiusZ, float tiltCos, float tiltSin)
        {
            return Mathf.Max(0.0001f, Mathf.Abs(radiusX * tiltSin) + Mathf.Abs(radiusZ * tiltCos));
        }

        private static Vector3 FallbackOrbitPosition(ViviFairy fairy)
        {
            return fairy != null ? fairy.RealPosition.Yto0() : Vector3.zero;
        }
    }
}
