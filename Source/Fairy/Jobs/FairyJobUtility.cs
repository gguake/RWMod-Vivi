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

            float angle = OrbitAngle(props, GenTicks.TicksGame, slot, count);
            float cos = Mathf.Cos(angle);
            float sin = Mathf.Sin(angle);
            return centerPawn.DrawPos.Yto0() + new Vector3(cos * props.orbitRadiusX, 0f, sin * props.orbitRadiusZ);
        }

        public static void IdleOrbitAround(ViviFairy fairy, Pawn centerPawn, int slot, int count)
        {
            if (fairy == null || fairy.Destroyed || fairy.State != FairyState.Idle || fairy.Map == null) { return; }

            var props = fairy.Controller?.Props;
            if (props == null || centerPawn == null || !centerPawn.Spawned || centerPawn.Map != fairy.Map) { return; }

            float angle = OrbitAngle(props, GenTicks.TicksGame, slot, count);
            float cos = Mathf.Cos(angle);
            float sin = Mathf.Sin(angle);

            var orbit = centerPawn.DrawPos.Yto0() + new Vector3(cos * props.orbitRadiusX, 0f, sin * props.orbitRadiusZ);
            var tangent = new Vector3(-sin * props.orbitRadiusX, 0f, cos * props.orbitRadiusZ);
            var drawAltitude = OrbitDrawAltitude(orbit, centerPawn.DrawPos, props);

            fairy.SetToilPosition(orbit, tangent, drawAltitude);
        }

        private static float OrbitAngle(CompProperties_ViviFairyController props, float clockTicks, int slot, int count)
        {
            return clockTicks * props.orbitAngularSpeed + slot * (Mathf.PI * 2f / Mathf.Max(1, count));
        }

        private static float OrbitDrawAltitude(Vector3 orbit, Vector3 center, CompProperties_ViviFairyController props)
        {
            float radiusZ = Mathf.Max(0.0001f, props.orbitRadiusZ);
            float rel = Mathf.Clamp((center.z - orbit.z) / radiusZ, -1f, 1f);
            float t = 0.5f + 0.5f * rel;
            return center.y + Mathf.Lerp(-props.orbitDepth, props.orbitDepth, t);
        }
    }
}
