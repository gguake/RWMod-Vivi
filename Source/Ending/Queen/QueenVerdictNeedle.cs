using RimWorld;
using RPEF;
using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace VVRace
{
    // 아포크리파 바늘은 보스 상태에 묶여 있으므로 그리기·연출만 공유한다.
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

        // ApocryphaNeedle과 같은 그림을 쓰므로 그림 중심에서 촉까지의 거리도 같다.
        private const float NeedleTipOffset = 1.98f;
        private const float FireSpeed = 7f;
        private const float WindupDistance = 0.8f;
        private const int ShakeTicks = 20;
        private const float ShakeAmplitude = 0.08f;
        private const float PawnHitRadius = 0.35f;
        private const float TrailPointSpacing = 1f;
        private const int AimPetalInterval = 4;
        private const float MapEdgeMargin = 0.05f;

        private static readonly List<Thing> _tmpCellThings = new List<Thing>();

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

            hover = ClampInsideMap(map, origin - forward * BehindDistance + new Vector3(0f, 0f, HoverLift));

            direction = (target - hover).Yto0();
            direction = direction.sqrMagnitude > 0.01f ? direction.normalized : forward;

            laneStart = ClampInsideMap(map, hover + direction * NeedleTipOffset);
            length = LaneLengthInsideMap(map, laneStart, direction, LaneRange);
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

            PlayEffectAt(QueenDefOf.VV_Effecter_QueenVerdictSummon, _hover);
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

            if (Scribe.mode == LoadSaveMode.PostLoadInit && _hitIds == null)
            {
                _hitIds = new List<int>();
            }
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
                        PlayEffectAt(ApocryphaEffecterDefOf.VV_Effecter_ApocryphaNeedleAim, _pos);
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
                ThrowAimPetals(progress);
            }

            if (elapsed >= AimTicks)
            {
                Fire(now);
            }
        }

        private void ThrowAimPetals(float progress)
        {
            var map = Map;
            var filled = _laneLength * Mathf.Max(0.15f, progress);
            if (!PointOnLane(filled * 0.5f).ShouldSpawnMotesAt(map)) { return; }

            var perpendicular = new Vector3(_laneDir.z, 0f, -_laneDir.x);
            for (int i = 0; i < 2; i++)
            {
                var pos = PointOnLane(Rand.Range(0f, filled)) + perpendicular * Rand.Range(-HalfWidth, HalfWidth) * 0.85f;
                var def = i == 0 ? ApocryphaFleckDefOf.VV_Fleck_ApocryphaAimSpark : ApocryphaFleckDefOf.VV_Fleck_ApocryphaAimTwinkle;
                var data = FleckMaker.GetDataStatic(pos, map, def, i == 0 ? Rand.Range(0.25f, 0.45f) : Rand.Range(0.2f, 0.35f));
                data.velocityAngle = LaneAngle + Rand.Range(-20f, 20f);
                data.velocitySpeed = Rand.Range(0.5f, 1.3f);
                data.rotation = Rand.Range(0f, 360f);
                data.rotationRate = Rand.Range(-120f, 120f);
                map.flecks.CreateFleck(data);
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
            RegisterTrail(_pos);

            PlayEffectAt(ApocryphaEffecterDefOf.VV_Effecter_ApocryphaNeedleFire, _pos);
            ApocryphaUtility.ShakeCamera(Map, 0.5f);

            TickFire(now);
        }

        // _travelled는 경로 시작점에서 촉까지의 거리다. 그림 중심은 촉보다 NeedleTipOffset만큼 뒤에 있다.
        private void TickFire(int now)
        {
            var from = _travelled;
            var to = Mathf.Min(_laneLength, _travelled + FireSpeed);

            for (var d = from + TrailPointSpacing; d < to; d += TrailPointSpacing)
            {
                RegisterTrail(BodyOnLane(d));
            }

            _travelled = to;
            _pos = BodyOnLane(to);
            RegisterTrail(_pos);

            DamageAlong(Mathf.Max(0f, from), to);
            if (Destroyed) { return; }

            if (to >= _laneLength)
            {
                _state = VerdictState.End;
                _stateStartTick = now;
                PlayEffectAt(ApocryphaEffecterDefOf.VV_Effecter_ApocryphaNeedleEnd, PointOnLane(_laneLength));
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
                PlayEffectAt(ApocryphaEffecterDefOf.VV_Effecter_ApocryphaNeedleVanish, _pos);
                Destroy();
            }
        }

        private void DamageAlong(float from, float to)
        {
            var map = Map;
            if (map == null || to <= from) { return; }

            var cellExtent = 0.5f * (Mathf.Abs(_laneDir.x) + Mathf.Abs(_laneDir.z));
            var margin = HalfWidth + 2f;
            var a = PointOnLane(from);
            var b = PointOnLane(to);
            var rect = CellRect.FromLimits(
                Mathf.FloorToInt(Mathf.Min(a.x, b.x) - margin),
                Mathf.FloorToInt(Mathf.Min(a.z, b.z) - margin),
                Mathf.CeilToInt(Mathf.Max(a.x, b.x) + margin),
                Mathf.CeilToInt(Mathf.Max(a.z, b.z) + margin)).ClipInsideMap(map);

            foreach (var cell in rect)
            {
                _tmpCellThings.Clear();
                _tmpCellThings.AddRange(cell.GetThingList(map));
                for (int i = 0; i < _tmpCellThings.Count; i++)
                {
                    var thing = _tmpCellThings[i];
                    if (!CanPierce(thing)) { continue; }

                    var hit = thing is Pawn
                        ? OverlapsLane(thing.DrawPos, PawnHitRadius, PawnHitRadius, from, to)
                        : OverlapsLane(cell.ToVector3Shifted(), cellExtent, cellExtent, from, to);
                    if (!hit) { continue; }

                    _hitIds.Add(thing.thingIDNumber);
                    Pierce(thing);
                }
            }

            _tmpCellThings.Clear();
        }

        // 틱 사이의 중복 판정을 피하도록 띠 양 끝에서는 일부만 걸쳐도 허용하고 중간 구간에서는 중심이 포함돼야 한다.
        private bool OverlapsLane(Vector3 point, float extentAcross, float extentAlong, float from, float to)
        {
            var rel = (point - _laneStart).Yto0();
            var along = rel.x * _laneDir.x + rel.z * _laneDir.z;
            var lo = from <= 0f ? -extentAlong : from;
            var hi = to >= _laneLength ? _laneLength + extentAlong : to;
            if (along < lo || along > hi) { return false; }

            return Mathf.Abs(rel.x * _laneDir.z - rel.z * _laneDir.x) <= HalfWidth + extentAcross;
        }

        private bool CanPierce(Thing thing)
        {
            if (thing.Destroyed || !thing.Spawned || thing == _caster || _hitIds.Contains(thing.thingIDNumber)) { return false; }

            if (thing is Pawn pawn)
            {
                if (pawn.Dead || pawn.Downed) { return false; }
            }
            else if (!(thing is Building building) || !building.def.useHitPoints)
            {
                return false;
            }

            return _faction != null && thing.HostileTo(_faction);
        }

        private void Pierce(Thing thing)
        {
            var map = thing.Map;
            var cell = thing.Position;
            var drawPos = thing.DrawPos;

            var dinfo = new DamageInfo(ApocryphaDamageDefOf.VV_GiantNeedlePierce, Damage, ArmorPenetration, LaneAngle, _caster);
            thing.TakeDamage(dinfo);

            SpawnPierceEffect(map, cell, drawPos);
        }

        private void SpawnPierceEffect(Map map, IntVec3 cell, Vector3 drawPos)
        {
            if (map == null || !cell.InBounds(map)) { return; }

            ApocryphaGimmicks.PlayEffect(ApocryphaEffecterDefOf.VV_Effecter_ApocryphaNeedleHit, cell, map, OffsetFromCell(drawPos, cell));
            if (!cell.ShouldSpawnMotesAt(map)) { return; }

            var basePos = drawPos.Yto0();
            basePos.y = AltitudeLayer.MoteOverhead.AltitudeFor();
            var angle = LaneAngle;

            var pierce = FleckMaker.GetDataStatic(basePos, map, VVFleckDefOf.VV_Fleck_NeedlePierce, Rand.Range(2.2f, 2.6f));
            pierce.rotation = angle;
            map.flecks.CreateFleck(pierce);

            var shardCount = Rand.RangeInclusive(6, 9);
            for (int i = 0; i < shardCount; i++)
            {
                var offset = new Vector3(Rand.Range(-0.2f, 0.2f), 0f, Rand.Range(-0.2f, 0.2f));
                var shard = FleckMaker.GetDataStatic(basePos + offset, map, VVFleckDefOf.VV_Fleck_NeedleShard, Rand.Range(0.45f, 0.8f));
                shard.velocityAngle = angle + Rand.Range(-55f, 55f);
                shard.velocitySpeed = Rand.Range(2f, 4.5f);
                shard.rotation = shard.velocityAngle + Rand.Range(-25f, 25f);
                shard.rotationRate = Rand.Range(-360f, 360f);
                map.flecks.CreateFleck(shard);
            }
        }

        protected override void DrawAt(Vector3 drawLoc, bool flip = false)
        {
            var now = GenTicks.TicksGame;
            var elapsedSecs = (now - _stateStartTick) / 60f;
            var alpha = _state == VerdictState.Summon ? Mathf.Clamp01((now - _stateStartTick) / (float)SummonTicks) : 1f;

            if (_state == VerdictState.Aim)
            {
                var progress = Mathf.Clamp01((now - _stateStartTick) / (float)AimTicks);
                ApocryphaNeedleRenderer.DrawLane(_laneStart, _laneDir, _laneLength, HalfWidth * 2f, progress, 1f, false, elapsedSecs, AimTicks / 60f, thingIDNumber);
            }

            var warpAge = now - _fireTick;
            if (_fireTick >= 0 && warpAge >= 0 && warpAge < WarpTicks)
            {
                ApocryphaNeedleRenderer.DrawWarp(_laneStart, _laneDir, _laneLength, Mathf.Max(0f, _travelled), warpAge / 60f, 1f);
            }

            var glow = _state == VerdictState.Summon ? 0.3f : _state == VerdictState.Aim ? Mathf.Clamp01((now - _stateStartTick) / (float)AimTicks) : 1f;
            ApocryphaNeedleRenderer.DrawNeedle(DrawPos, LaneAngle, alpha, glow);
        }

        private Vector3 PointOnLane(float distance)
        {
            return _laneStart + _laneDir * distance;
        }

        private Vector3 BodyOnLane(float tipDistance)
        {
            return PointOnLane(tipDistance - NeedleTipOffset);
        }

        private static float LaneLengthInsideMap(Map map, Vector3 start, Vector3 direction, float range)
        {
            var size = map.Size;
            var length = range;
            length = Mathf.Min(length, DistanceToBound(start.x, direction.x, size.x));
            length = Mathf.Min(length, DistanceToBound(start.z, direction.z, size.z));
            return Mathf.Max(0f, length);
        }

        private static float DistanceToBound(float start, float direction, int size)
        {
            if (direction > 0.0001f) { return (size - MapEdgeMargin - start) / direction; }
            if (direction < -0.0001f) { return (MapEdgeMargin - start) / direction; }

            return float.MaxValue;
        }

        private static Vector3 ClampInsideMap(Map map, Vector3 point)
        {
            var size = map.Size;
            point.x = Mathf.Clamp(point.x, MapEdgeMargin, size.x - MapEdgeMargin);
            point.z = Mathf.Clamp(point.z, MapEdgeMargin, size.z - MapEdgeMargin);
            return point;
        }

        private void SyncPosition()
        {
            var cell = _pos.ToIntVec3().ClampInsideMap(Map);
            if (cell != Position)
            {
                Position = cell;
            }
        }

        private void RegisterTrail(Vector3 point)
        {
            point.y = AltitudeLayer.MoteOverhead.AltitudeFor() - 0.01f;
            Trail?.RegisterNewTrail(point);
        }

        private void PlayEffectAt(EffecterDef effecterDef, Vector3 point)
        {
            var cell = point.ToIntVec3().ClampInsideMap(Map);
            ApocryphaGimmicks.PlayEffect(effecterDef, cell, Map, OffsetFromCell(point, cell));
        }

        private static Vector3 OffsetFromCell(Vector3 point, IntVec3 cell)
        {
            return (point - cell.ToVector3Shifted()).WithY(0f);
        }
    }
}
