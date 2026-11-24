using UnityEngine;
using Verse;

namespace VVRace
{
    public class FairyToil_AsphodelBondOrbit : FairyToil
    {
        public const float StartRadiusX = 2.8f;
        public const float StartRadiusZ = 2.0f;
        public const float LiftStart = 0.25f;
        public const float MinAngularSpeed = 0.022f;
        public const float MaxAngularSpeed = 0.058f;

        private const float DepthOffset = 0.08f;
        private const float BobAmplitude = 0.12f;
        private const float ReturnSpeed = 0.25f;

        private Pawn _target;
        private int _slot;
        private int _count;
        private bool _playPhaseEffect;
        private bool _smoothReturn;

        public bool IsReturning => _smoothReturn;

        public FairyToil_AsphodelBondOrbit() { }

        public FairyToil_AsphodelBondOrbit(Pawn target, int slot, int count, bool playPhaseEffect = true, bool smoothReturn = false)
        {
            _target = target;
            _slot = slot;
            _count = Mathf.Max(1, count);
            _playPhaseEffect = playPhaseEffect;
            _smoothReturn = smoothReturn;
        }

        protected override void OnStarted()
        {
            var fairy = Fairy;
            if (fairy == null || !fairy.Spawned) { return; }

            fairy.EnterIdle();
            if (_playPhaseEffect && VVEffecterDefOf.VV_Effecter_FairyPhase != null && fairy.Position.InBounds(fairy.Map))
            {
                VVEffecterDefOf.VV_Effecter_FairyPhase.Spawn(fairy.Position, fairy.Map).Cleanup();
            }

            if (!_smoothReturn)
            {
                UpdateOrbit(0);
            }
        }

        protected override FairyToilStatus TickAction(int delta)
        {
            var fairy = Fairy;
            if (fairy == null || fairy.Destroyed || !fairy.Spawned) { return FairyToilStatus.Complete; }

            if (fairy.State != FairyState.Idle)
            {
                fairy.EnterIdle();
            }

            if (_target == null || !_target.Spawned || _target.Dead || _target.Map != fairy.Map)
            {
                return FairyToilStatus.Complete;
            }

            UpdateOrbit(delta);
            return FairyToilStatus.Running;
        }

        private void UpdateOrbit(int delta)
        {
            var fairy = Fairy;
            if (fairy == null || _target == null || !_target.Spawned) { return; }

            var bond = _target.health?.hediffSet?.GetFirstHediffOfDef(VVHediffDefOf.VV_AsphodelBond) as Hediff_AsphodelBond;
            var progress = bond?.Progress ?? 1f;
            var shrink = Mathf.Clamp01(1f - progress);
            var angle = (job as FairyJob_AsphodelBondOrbit)?.OrbitAngle ?? (_slot * (Mathf.PI * 2f / Mathf.Max(1, _count)));

            var radiusX = StartRadiusX * shrink;
            var radiusZ = StartRadiusZ * shrink;
            var bob = BobAmplitude * shrink * Mathf.Sin(GenTicks.TicksGame * 0.08f + _slot * 1.3f);

            var cos = Mathf.Cos(angle);
            var sin = Mathf.Sin(angle);
            var targetDraw = _target.DrawPos;
            var center = targetDraw.Yto0() + new Vector3(0f, 0f, LiftStart * shrink + bob);
            var offset = new Vector3(cos * radiusX, 0f, sin * radiusZ);
            var tangent = new Vector3(-sin * Mathf.Max(0.1f, radiusX), 0f, cos * Mathf.Max(0.1f, radiusZ));

            var depthRatio = radiusZ > 0.01f ? Mathf.Clamp(-offset.z / radiusZ, -1f, 1f) : 1f;
            var drawAltitude = targetDraw.y + Mathf.Lerp(-DepthOffset, DepthOffset, 0.5f + 0.5f * depthRatio);
            var desired = center + offset;

            if (_smoothReturn)
            {
                if (delta <= 0) { return; }

                var current = fairy.RealPosition;
                var toGoal = desired - current;
                var step = ReturnSpeed * delta;
                if (toGoal.sqrMagnitude <= step * step)
                {
                    _smoothReturn = false;
                    fairy.SetToilPosition(desired, tangent, drawAltitude);
                }
                else
                {
                    var next = current + toGoal.normalized * step;
                    fairy.SetToilPosition(next, toGoal, AltitudeLayer.MoteOverhead.AltitudeFor());
                }
                return;
            }

            fairy.SetToilPosition(desired, tangent, drawAltitude);
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_References.Look(ref _target, "target");
            Scribe_Values.Look(ref _slot, "slot", 0);
            Scribe_Values.Look(ref _count, "count", 1);
            Scribe_Values.Look(ref _playPhaseEffect, "playPhaseEffect", false);
            Scribe_Values.Look(ref _smoothReturn, "smoothReturn", false);
        }
    }
}
