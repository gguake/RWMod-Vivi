using RimWorld;
using RPEF;
using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace VVRace
{
    public class QueenVerdictNeedle : ThingWithComps
    {
        private enum VerdictState : byte
        {
            Summon = 0,
            Aim = 1,
            Fire = 2,
            End = 3,
        }

        public const float LaneRange = 35f;
        public const float HalfWidth = 1.5f;
        public const float Damage = 80f;
        public const float ArmorPenetration = 0.8f;

        private const int SummonTicks = 40;
        private const int AimTicks = 60;
        private const int EndTicks = 16;
        private const int WarpTicks = 33;

        private const float BehindDistance = 2f;
        private const float HoverLift = 1.2f;
        private const float HoverBob = 0.1f;

        private const float FireSpeed = 7f;
        private const float WindupDistance = 0.8f;
        private const int ShakeTicks = 20;
        private const float ShakeAmplitude = 0.08f;
        private const int AimPetalInterval = 4;

        private Pawn _caster;
        private Faction _faction;
        private VerdictState _state;
        private int _stateStartTick;
        private int _fireTick = -1;
        private Vector3 _pos;
        private Vector3 _hover;
        private Vector3 _laneStart;
        private Vector3 _laneDir = Vector3.forward;
        private float _laneLength;
        private float _travelled;
        private List<int> _hitIds = new List<int>();

        private CompTrailRenderer _trail;

        private CompTrailRenderer Trail => _trail ?? (_trail = GetComp<CompTrailRenderer>());

        private float LaneAngle => _laneDir.AngleFlat();

        public override Vector3 DrawPos => new Vector3(_pos.x, AltitudeLayer.MoteOverhead.AltitudeFor(), _pos.z);

        public void Init(Pawn caster, IntVec3 targetCell)
        {
            _caster = caster;
            _faction = caster.Faction;
            ComputeLane(caster, targetCell, out _hover, out _laneStart, out _laneDir, out _laneLength);
            _pos = _hover;
        }

        public static void ComputeLane(Pawn caster, IntVec3 targetCell, out Vector3 hover, out Vector3 laneStart, out Vector3 direction, out float length)
        {
            var map = caster.Map;
            var origin = caster.DrawPos.Yto0();
            var target = targetCell.ToVector3Shifted().Yto0();
            var toTarget = (target - origin).Yto0();
            var forward = toTarget.sqrMagnitude > 0.01f ? toTarget.normalized : caster.Rotation.FacingCell.ToVector3();

            hover = ApocryphaNeedleLane.ClampInsideMap(map, origin - forward * BehindDistance + new Vector3(0f, 0f, HoverLift));

            direction = (target - hover).Yto0();
            direction = direction.sqrMagnitude > 0.01f ? direction.normalized : forward;

            laneStart = ApocryphaNeedleLane.ClampInsideMap(map, hover + direction * ApocryphaNeedleLane.TipOffset);
            length = ApocryphaNeedleLane.LengthInsideMap(map, laneStart, direction, LaneRange);
        }

        public static IEnumerable<IntVec3> LaneCells(Map map, Vector3 laneStart, Vector3 direction, float length)
        {
            var end = laneStart + direction * length;
            var margin = HalfWidth + 1f;
            var rect = CellRect.FromLimits(
                Mathf.FloorToInt(Mathf.Min(laneStart.x, end.x) - margin),
                Mathf.FloorToInt(Mathf.Min(laneStart.z, end.z) - margin),
                Mathf.CeilToInt(Mathf.Max(laneStart.x, end.x) + margin),
                Mathf.CeilToInt(Mathf.Max(laneStart.z, end.z) + margin)).ClipInsideMap(map);

            foreach (var cell in rect)
            {
                var rel = (cell.ToVector3Shifted() - laneStart).Yto0();
                var along = rel.x * direction.x + rel.z * direction.z;
                if (along < 0f || along > length) { continue; }
                if (Mathf.Abs(rel.x * direction.z - rel.z * direction.x) > HalfWidth) { continue; }

                yield return cell;
            }
        }

        public override void SpawnSetup(Map map, bool respawningAfterLoad)
        {
            base.SpawnSetup(map, respawningAfterLoad);
            if (respawningAfterLoad) { return; }

            _state = VerdictState.Summon;
            _stateStartTick = GenTicks.TicksGame;
            SyncPosition();

            PlayEffectAt(VVEffecterDefOf.VV_Effecter_QueenVerdictSummon, _hover);
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_References.Look(ref _caster, "caster");
            Scribe_References.Look(ref _faction, "faction");
            Scribe_Values.Look(ref _state, "state", VerdictState.Summon);
            Scribe_Values.Look(ref _stateStartTick, "stateStartTick", 0);
            Scribe_Values.Look(ref _fireTick, "fireTick", -1);
            Scribe_Values.Look(ref _pos, "pos");
            Scribe_Values.Look(ref _hover, "hover");
            Scribe_Values.Look(ref _laneStart, "laneStart");
            Scribe_Values.Look(ref _laneDir, "laneDir", Vector3.forward);
            Scribe_Values.Look(ref _laneLength, "laneLength", 0f);
            Scribe_Values.Look(ref _travelled, "travelled", 0f);
            Scribe_Collections.Look(ref _hitIds, "hitIds", LookMode.Value);
        }

        protected override void Tick()
        {
            base.Tick();
            if (!Spawned) { return; }

            var now = GenTicks.TicksGame;
            var elapsed = now - _stateStartTick;
            switch (_state)
            {
                case VerdictState.Summon:
                    _pos = _hover + new Vector3(0f, 0f, HoverBob * Mathf.Sin(elapsed * 0.08f));
                    if (elapsed >= SummonTicks)
                    {
                        _state = VerdictState.Aim;
                        _stateStartTick = now;
                        PlayEffectAt(VVEffecterDefOf.VV_Effecter_ApocryphaNeedleAim, _pos);
                    }
                    break;

                case VerdictState.Aim:
                    TickAim(now, elapsed);
                    break;

                case VerdictState.Fire:
                    TickFire(now);
                    break;

                case VerdictState.End:
                    TickEnd(elapsed);
                    break;
            }

            if (Spawned)
            {
                SyncPosition();
            }
        }

        private void TickAim(int now, int elapsed)
        {
            var progress = Mathf.Clamp01(elapsed / (float)AimTicks);
            var hold = _hover - _laneDir * (WindupDistance * Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.4f, 1f, progress)));
            if (AimTicks - elapsed <= ShakeTicks)
            {
                var perpendicular = new Vector3(_laneDir.z, 0f, -_laneDir.x);
                hold += perpendicular * (ShakeAmplitude * Rand.Range(-1f, 1f));
            }

            _pos = hold;

            if (elapsed % AimPetalInterval == 0)
            {
                ApocryphaNeedleLane.ThrowAimPetals(Map, _laneStart, _laneDir, _laneLength, HalfWidth, progress);
            }

            if (elapsed >= AimTicks)
            {
                Fire(now);
            }
        }

        private void Fire(int now)
        {
            _state = VerdictState.Fire;
            _stateStartTick = now;
            _fireTick = now;
            _travelled = -WindupDistance;
            _hitIds.Clear();

            _pos = BodyOnLane(_travelled);
            Trail?.ResetTrail();
            ApocryphaNeedleLane.RegisterTrail(Trail, _pos);

            PlayEffectAt(VVEffecterDefOf.VV_Effecter_ApocryphaNeedleFire, _pos);
            ApocryphaUtility.ShakeCamera(Map, 0.5f);

            TickFire(now);
        }

        private void TickFire(int now)
        {
            var from = _travelled;
            var to = Mathf.Min(_laneLength, _travelled + FireSpeed);

            ApocryphaNeedleLane.RegisterTrailAlong(Trail, _laneStart, _laneDir, from, to);
            _travelled = to;
            _pos = BodyOnLane(to);

            ApocryphaNeedleLane.PierceAlong(this, _laneStart, _laneDir, _laneLength, HalfWidth, Mathf.Max(0f, from), to, _hitIds, CanPierce, Pierce);
            if (Destroyed) { return; }

            if (to >= _laneLength)
            {
                _state = VerdictState.End;
                _stateStartTick = now;
                var laneEnd = _laneStart + _laneDir * _laneLength;
                PlayEffectAt(VVEffecterDefOf.VV_Effecter_ApocryphaNeedleEnd, laneEnd);
            }
        }

        private void TickEnd(int elapsed)
        {
            var shake = ShakeAmplitude * (1f - elapsed / (float)EndTicks);
            var perpendicular = new Vector3(_laneDir.z, 0f, -_laneDir.x);
            _pos = BodyOnLane(_laneLength) + perpendicular * (shake * Rand.Range(-1f, 1f));

            if (elapsed >= EndTicks)
            {
                Trail?.FinishTrail();
                PlayEffectAt(VVEffecterDefOf.VV_Effecter_ApocryphaNeedleVanish, _pos);
                Destroy();
            }
        }

        private bool CanPierce(Thing thing)
        {
            return thing != _caster && _faction != null && thing.HostileTo(_faction);
        }

        private void Pierce(Thing thing)
        {
            ApocryphaNeedleLane.Pierce(thing, Damage, ArmorPenetration, LaneAngle, _caster);
        }

        protected override void DrawAt(Vector3 drawLoc, bool flip = false)
        {
            var now = GenTicks.TicksGame;
            var elapsedSecs = (now - _stateStartTick) / 60f;
            var alpha = _state == VerdictState.Summon ? Mathf.Clamp01((now - _stateStartTick) / (float)SummonTicks) : 1f;

            if (_state == VerdictState.Aim)
            {
                var progress = Mathf.Clamp01((now - _stateStartTick) / (float)AimTicks);
                ApocryphaNeedleRenderer.DrawLane(
                    start: _laneStart, direction: _laneDir, length: _laneLength, width: HalfWidth * 2f,
                    progress: progress, alpha: 1f, bossInside: false, breach: false,
                    aimSecs: elapsedSecs, aimDuration: AimTicks / 60f, seed: thingIDNumber);
            }

            var warpAge = now - _fireTick;
            if (_fireTick >= 0 && warpAge >= 0 && warpAge < WarpTicks)
            {
                ApocryphaNeedleRenderer.DrawWarp(_laneStart, _laneDir, _laneLength, Mathf.Max(0f, _travelled), warpAge / 60f, 1f);
            }

            var glow = _state == VerdictState.Summon ? 0.3f : _state == VerdictState.Aim ? Mathf.Clamp01((now - _stateStartTick) / (float)AimTicks) : 1f;
            ApocryphaNeedleRenderer.DrawNeedle(DrawPos, LaneAngle, alpha, glow);
        }

        private Vector3 BodyOnLane(float tipDistance)
        {
            return ApocryphaNeedleLane.BodyOnLane(_laneStart, _laneDir, tipDistance);
        }

        private void SyncPosition()
        {
            var cell = _pos.ToIntVec3().ClampInsideMap(Map);
            if (cell != Position)
            {
                Position = cell;
            }
        }

        private void PlayEffectAt(EffecterDef effecterDef, Vector3 point)
        {
            ApocryphaNeedleLane.PlayEffectAt(Map, effecterDef, point);
        }
    }
}
