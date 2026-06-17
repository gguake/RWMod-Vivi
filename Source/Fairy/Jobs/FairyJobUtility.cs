using UnityEngine;
using Verse;

namespace VVRace
{
    internal static class FairyJobUtility
    {
        public static Vector3 OrbitPositionAround(ViviFairy fairy, Pawn centerPawn, int slot, int count)
        {
            var props = fairy?.Controller?.Props;
            if (props == null || fairy == null || centerPawn == null || !centerPawn.Spawned || centerPawn.Map != fairy.Map)
            {
                return fairy != null ? fairy.RealPosition.Yto0() : Vector3.zero;
            }

            float angle = OrbitAngle(fairy, props, GenTicks.TicksGame, slot, count);
            float cos = Mathf.Cos(angle);
            float sin = Mathf.Sin(angle);
            float radiusX = props.orbitRadiusX * fairy.OrbitRadiusXFactor;
            float radiusZ = props.orbitRadiusZ * fairy.OrbitRadiusZFactor;
            return centerPawn.DrawPos.Yto0() + RotateOrbitVector(new Vector3(cos * radiusX, 0f, sin * radiusZ), fairy.OrbitTiltAngle);
        }

        public static void IdleOrbitAround(ViviFairy fairy, Pawn centerPawn, int slot, int count)
        {
            if (fairy == null || fairy.Destroyed || fairy.State != FairyState.Idle || fairy.Map == null) { return; }

            var props = fairy.Controller?.Props;
            if (props == null || centerPawn == null || !centerPawn.Spawned || centerPawn.Map != fairy.Map) { return; }

            float angle = OrbitAngle(fairy, props, GenTicks.TicksGame, slot, count);
            float cos = Mathf.Cos(angle);
            float sin = Mathf.Sin(angle);
            float radiusX = props.orbitRadiusX * fairy.OrbitRadiusXFactor;
            float radiusZ = props.orbitRadiusZ * fairy.OrbitRadiusZFactor;

            var orbit = centerPawn.DrawPos.Yto0() + RotateOrbitVector(new Vector3(cos * radiusX, 0f, sin * radiusZ), fairy.OrbitTiltAngle);
            var tangent = RotateOrbitVector(new Vector3(-sin * radiusX, 0f, cos * radiusZ), fairy.OrbitTiltAngle) * fairy.OrbitDirection;
            var drawAltitude = OrbitDrawAltitude(orbit, centerPawn.DrawPos, props);

            fairy.SetToilPosition(orbit, tangent, drawAltitude);
        }
        private static float OrbitDrawAltitude(Vector3 orbit, Vector3 center, CompProperties_ViviFairyController props)
        {
            float radiusZ = Mathf.Max(0.0001f, props.orbitRadiusZ);
            float rel = Mathf.Clamp((center.z - orbit.z) / radiusZ, -1f, 1f);
            float t = 0.5f + 0.5f * rel;
            return center.y + Mathf.Lerp(-props.orbitDepth, props.orbitDepth, t);
        }

        private static Vector3 RotateOrbitVector(Vector3 vector, float angle)
        {
            float cos = Mathf.Cos(angle);
            float sin = Mathf.Sin(angle);
            return new Vector3(
                vector.x * cos - vector.z * sin,
                0f,
                vector.x * sin + vector.z * cos);
        }

        private static float OrbitAngle(ViviFairy fairy, CompProperties_ViviFairyController props, float clockTicks, int slot, int count)
        {
            float slotOffset = slot * (Mathf.PI * 2f / Mathf.Max(1, count));
            float motion = clockTicks * props.orbitAngularSpeed * fairy.OrbitSpeedFactor * fairy.OrbitDirection;
            return motion + slotOffset + fairy.OrbitPhaseOffset;
        }

    }
}
