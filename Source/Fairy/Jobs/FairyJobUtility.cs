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

        public static Vector3 OrbitPositionAround(ViviFairy fairy, Pawn centerPawn, int slot, int count)
        {
            var props = fairy?.Controller?.Props;
            if (props == null || fairy == null || centerPawn == null || !centerPawn.Spawned || centerPawn.Map != fairy.Map)
            {
                return fairy != null ? fairy.RealPosition.Yto0() : Vector3.zero;
            }

            float slotOffset = slot * (Mathf.PI * 2f / Mathf.Max(1, count));
            float motion = GenTicks.TicksGame * IdleOrbitAngularSpeed * fairy.OrbitSpeedFactor * fairy.OrbitDirection;
            float angle = motion + slotOffset + fairy.OrbitPhaseOffset;
            float cos = Mathf.Cos(angle);
            float sin = Mathf.Sin(angle);
            float radiusX = IdleOrbitRadiusX * fairy.OrbitRadiusXFactor;
            float radiusZ = IdleOrbitRadiusZ * fairy.OrbitRadiusZFactor;

            var vector = new Vector3(cos * radiusX, 0f, sin * radiusZ);
            float tiltCos = Mathf.Cos(fairy.OrbitTiltAngle);
            float tiltSin = Mathf.Sin(fairy.OrbitTiltAngle);
            var rotated = new Vector3(
                vector.x * tiltCos - vector.z * tiltSin,
                0f,
                vector.x * tiltSin + vector.z * tiltCos);
            return centerPawn.DrawPos.Yto0() + rotated;
        }

        public static void IdleOrbitAround(ViviFairy fairy, Pawn centerPawn, int slot, int count)
        {
            if (fairy == null || fairy.Destroyed || fairy.State != FairyState.Idle || fairy.Map == null) { return; }

            var props = fairy.Controller?.Props;
            if (props == null || centerPawn == null || !centerPawn.Spawned || centerPawn.Map != fairy.Map) { return; }

            float slotOffset = slot * (Mathf.PI * 2f / Mathf.Max(1, count));
            float motion = GenTicks.TicksGame * IdleOrbitAngularSpeed;
            float angle = motion + slotOffset;
            float cos = Mathf.Cos(angle);
            float sin = Mathf.Sin(angle);
            float radiusX = IdleOrbitRadiusX;
            float radiusZ = IdleOrbitRadiusZ;

            var center = centerPawn.DrawPos;
            var orbit = center.Yto0() + new Vector3(cos * radiusX, 0f, sin * radiusZ);
            var tangent = new Vector3(-sin * radiusX, 0f, cos * radiusZ);
            float depthRadius = Mathf.Max(0.0001f, IdleOrbitRadiusZ);
            float rel = Mathf.Clamp((center.z - orbit.z) / depthRadius, -1f, 1f);
            float drawAltitude = center.y + Mathf.Lerp(-IdleOrbitDepth, IdleOrbitDepth, 0.5f + 0.5f * rel);

            fairy.SetToilPosition(orbit, tangent, drawAltitude);
        }

        public static Vector3 IdleOrbitPositionAround(ViviFairy fairy, Pawn centerPawn, int slot, int count)
        {
            var props = fairy?.Controller?.Props;
            if (props == null || fairy == null || centerPawn == null || !centerPawn.Spawned || centerPawn.Map != fairy.Map)
            {
                return fairy != null ? fairy.RealPosition.Yto0() : Vector3.zero;
            }

            float slotOffset = slot * (Mathf.PI * 2f / Mathf.Max(1, count));
            float motion = GenTicks.TicksGame * IdleOrbitAngularSpeed;
            float angle = motion + slotOffset;
            float cos = Mathf.Cos(angle);
            float sin = Mathf.Sin(angle);
            return centerPawn.DrawPos.Yto0() + new Vector3(cos * IdleOrbitRadiusX, 0f, sin * IdleOrbitRadiusZ);
        }
    }
}
