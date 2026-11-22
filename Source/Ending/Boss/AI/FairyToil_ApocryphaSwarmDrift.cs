using UnityEngine;
using Verse;

namespace VVRace
{
    public class FairyToil_ApocryphaSwarmDrift : FairyToil
    {
        private const float RadiusX = 3f;
        private const float RadiusZ = 2.1f;
        private const float Lift = 0.55f;
        private const float Wobble = 0.4f;
        private const float AngularSpeed = 0.022f;
        private const float Follow = 0.14f;
        private const float BehindAltitude = 0.04f;
        private const float BehindThreshold = 0.6f;

        protected override FairyToilStatus TickAction(int delta)
        {
            var fairy = Fairy;
            if (fairy == null || fairy.Destroyed || !fairy.Spawned) { return FairyToilStatus.Complete; }

            if (fairy.State == FairyState.Attacking)
            {
                fairy.EnterIdle();
            }

            var boss = job?.Owner;
            if (fairy.State != FairyState.Idle || boss == null || !boss.Spawned || boss.Map != fairy.Map || delta <= 0) { return FairyToilStatus.Running; }

            var current = fairy.RealPosition;
            if (boss.TryGetComp<CompApocrypha>() is CompApocrypha comp && comp.Embrace.TryGetFairyGoal(fairy, out var sigilGoal))
            {
                var toward = Vector3.Lerp(current, sigilGoal, Mathf.Clamp01(Follow * delta));
                fairy.SetToilPosition(toward, toward - current, AltitudeLayer.MoteOverhead.AltitudeFor());
                return FairyToilStatus.Running;
            }

            var ticks = GenTicks.TicksGame;
            var motion = ticks * (double)AngularSpeed * fairy.OrbitSpeedFactor * fairy.OrbitDirection + fairy.OrbitPhaseOffset;
            var angle = (float)(motion % (System.Math.PI * 2.0));
            var wobble = Wobble * Mathf.Sin(ticks * 0.05f + fairy.OrbitPhaseOffset * 3f);
            var sin = Mathf.Sin(angle);

            var center = ApocryphaGimmicks.GetBossBodyPos(boss).Yto0();
            var goal = center + new Vector3(
                Mathf.Cos(angle) * (RadiusX * fairy.OrbitRadiusXFactor + wobble),
                0f,
                sin * (RadiusZ * fairy.OrbitRadiusZFactor + wobble) + Lift);

            var next = Vector3.Lerp(current, goal, Mathf.Clamp01(Follow * delta));
            var altitude = sin > BehindThreshold ? boss.DrawPos.y - BehindAltitude : AltitudeLayer.MoteOverhead.AltitudeFor();
            fairy.SetToilPosition(next, next - current, altitude);
            return FairyToilStatus.Running;
        }
    }
}
