using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace VVRace
{
    public class FairyToil_AsphodelBondOrbit : FairyToil
    {
        public const float StartRadiusX = 2.8f;
        public const float StartRadiusZ = 2.0f;

        private const float DepthOffset = 0.08f;
        private const float LiftStart = 0.25f;
        private const float MinAngularSpeed = 0.022f;
        private const float MaxAngularSpeed = 0.058f;
        private const float BobAmplitude = 0.12f;

        private Pawn _target;
        private int _slot;
        private int _count;
        private float _angle;
        private bool _angleInitialized;

        public FairyToil_AsphodelBondOrbit() { }

        public FairyToil_AsphodelBondOrbit(Pawn target, int slot, int count)
        {
            _target = target;
            _slot = slot;
            _count = Mathf.Max(1, count);
            _angle = _slot * (Mathf.PI * 2f / _count);
            _angleInitialized = true;
        }

        protected override void OnStarted()
        {
            var fairy = Fairy;
            if (fairy == null || !fairy.Spawned) { return; }

            if (!_angleInitialized)
            {
                _angle = _slot * (Mathf.PI * 2f / Mathf.Max(1, _count));
                _angleInitialized = true;
            }

            fairy.EnterIdle();
            if (VVEffecterDefOf.VV_Effecter_FairyPhase != null && fairy.Position.InBounds(fairy.Map))
            {
                VVEffecterDefOf.VV_Effecter_FairyPhase.Spawn(fairy.Position, fairy.Map).Cleanup();
            }

            UpdateOrbit(0);
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

            if (delta > 0)
            {
                var angularSpeed = Mathf.Lerp(MinAngularSpeed, MaxAngularSpeed, progress);
                _angle = (_angle + angularSpeed * delta) % (Mathf.PI * 2f);
            }

            var radiusX = StartRadiusX * shrink;
            var radiusZ = StartRadiusZ * shrink;
            var bob = BobAmplitude * shrink * Mathf.Sin(GenTicks.TicksGame * 0.08f + _slot * 1.3f);

            var cos = Mathf.Cos(_angle);
            var sin = Mathf.Sin(_angle);
            var targetDraw = _target.DrawPos;
            var center = targetDraw.Yto0() + new Vector3(0f, 0f, LiftStart * shrink + bob);
            var offset = new Vector3(cos * radiusX, 0f, sin * radiusZ);
            var tangent = new Vector3(-sin * Mathf.Max(0.1f, radiusX), 0f, cos * Mathf.Max(0.1f, radiusZ));

            var depthRatio = radiusZ > 0.01f ? Mathf.Clamp(-offset.z / radiusZ, -1f, 1f) : 1f;
            var drawAltitude = targetDraw.y + Mathf.Lerp(-DepthOffset, DepthOffset, 0.5f + 0.5f * depthRatio);

            fairy.SetToilPosition(center + offset, tangent, drawAltitude);
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_References.Look(ref _target, "target");
            Scribe_Values.Look(ref _slot, "slot", 0);
            Scribe_Values.Look(ref _count, "count", 1);
            Scribe_Values.Look(ref _angle, "angle", 0f);
            Scribe_Values.Look(ref _angleInitialized, "angleInitialized", false);
        }
    }
}
