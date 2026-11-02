using RimWorld;
using RPEF;
using System;
using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace VVRace
{
    // 아스포델 바늘과 여왕의 심판 바늘이 공유하는 경로 계산·관통 판정·연출.
    internal static class ApocryphaNeedleLane
    {
        // 그림 중심에서 촉까지의 거리로 높이 5.5칸의 36%다.
        // 촉이 경고 띠를 벗어나지 않도록 몸통은 이 거리만큼 뒤에 그린다.
        public const float TipOffset = 1.98f;

        private const float PawnHitRadius = 0.35f;
        private const float TrailPointSpacing = 1f;
        private const float MapEdgeMargin = 0.05f;

        // 바닐라 Pawn_DrawTracker.FlyingOffset이 나는 폰을 그리는 높이(z칸).
        private const float FlightDrawLift = 0.6f;

        private static readonly List<Thing> _tmpCellThings = new List<Thing>();

        public static Vector3 BodyOnLane(Vector3 start, Vector3 direction, float tipDistance)
        {
            return start + direction * (tipDistance - TipOffset);
        }

        #region 맵 경계
        public static float LengthInsideMap(Map map, Vector3 start, Vector3 direction, float range)
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

        public static Vector3 ClampInsideMap(Map map, Vector3 point)
        {
            var size = map.Size;
            point.x = Mathf.Clamp(point.x, MapEdgeMargin, size.x - MapEdgeMargin);
            point.z = Mathf.Clamp(point.z, MapEdgeMargin, size.z - MapEdgeMargin);
            return point;
        }
        #endregion

        #region 관통
        // 촉이 [from, to] 구간을 지나는 동안 걸친 대상마다 pierce를 한 번씩 부른다. 바늘이 파괴되면 멈춘다.
        // 폰과 체력이 있는 건물만 대상이며 진영 조건은 canPierce가 정한다.
        public static void PierceAlong(Thing needle, Vector3 start, Vector3 direction, float length, float halfWidth, float from, float to,
            List<int> hitIds, Predicate<Thing> canPierce, Action<Thing> pierce)
        {
            var map = needle.Map;
            if (map == null || to <= from) { return; }

            var cellExtent = 0.5f * (Mathf.Abs(direction.x) + Mathf.Abs(direction.z));
            var margin = halfWidth + 2f;
            var a = start + direction * from;
            var b = start + direction * to;
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
                    if (hitIds.Contains(thing.thingIDNumber) || !IsPierceable(thing) || !canPierce(thing)) { continue; }

                    var hit = thing is Pawn pawn
                        ? Overlaps(start, direction, length, halfWidth, GroundPos(pawn), PawnHitRadius, PawnHitRadius, from, to)
                        : Overlaps(start, direction, length, halfWidth, cell.ToVector3Shifted(), cellExtent, cellExtent, from, to);
                    if (!hit) { continue; }

                    hitIds.Add(thing.thingIDNumber);
                    pierce(thing);
                    if (needle.Destroyed) { break; }
                }

                if (needle.Destroyed) { break; }
            }

            _tmpCellThings.Clear();
        }

        // 경고 띠는 땅에 그리므로 움직이는 폰은 그려지는 자리로, 나는 폰은 그 높이를 뺀 땅 위치로 판정한다.
        private static Vector3 GroundPos(Pawn pawn)
        {
            var pos = pawn.DrawPos;
            if (pawn.flight != null)
            {
                pos.z -= FlightDrawLift * pawn.flight.PositionOffsetFactor;
            }

            return pos;
        }

        private static bool IsPierceable(Thing thing)
        {
            if (thing.Destroyed || !thing.Spawned) { return false; }

            if (thing is Pawn pawn)
            {
                return !pawn.Dead && !pawn.Downed;
            }

            return thing is Building building && building.def.useHitPoints;
        }

        // 틱 사이의 중복 판정을 피하도록 띠 양 끝에서는 일부만 걸쳐도 허용하고 중간 구간에서는 중심이 포함돼야 한다.
        private static bool Overlaps(Vector3 start, Vector3 direction, float length, float halfWidth, Vector3 point, float extentAcross, float extentAlong, float from, float to)
        {
            var rel = (point - start).Yto0();
            var along = rel.x * direction.x + rel.z * direction.z;
            var lo = from <= 0f ? -extentAlong : from;
            var hi = to >= length ? length + extentAlong : to;
            if (along < lo || along > hi) { return false; }

            return Mathf.Abs(rel.x * direction.z - rel.z * direction.x) <= halfWidth + extentAcross;
        }

        public static void Pierce(Thing thing, float damage, float armorPenetration, float angle, Thing instigator)
        {
            var map = thing.Map;
            var cell = thing.Position;
            var drawPos = thing.DrawPos;

            thing.TakeDamage(new DamageInfo(VVDamageDefOf.VV_GiantNeedlePierce, damage, armorPenetration, angle, instigator));

            SpawnPierceEffect(map, cell, drawPos, angle);
        }

        private static void SpawnPierceEffect(Map map, IntVec3 cell, Vector3 drawPos, float angle)
        {
            if (map == null || !cell.InBounds(map)) { return; }

            ApocryphaGimmicks.PlayEffect(VVEffecterDefOf.VV_Effecter_ApocryphaNeedleHit, cell, map, OffsetFromCell(drawPos, cell));
            if (!cell.ShouldSpawnMotesAt(map)) { return; }

            var basePos = drawPos.Yto0();
            basePos.y = AltitudeLayer.MoteOverhead.AltitudeFor();

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
        #endregion

        #region 연출
        public static void ThrowAimPetals(Map map, Vector3 start, Vector3 direction, float length, float halfWidth, float progress)
        {
            var filled = length * Mathf.Max(0.15f, progress);
            if (!(start + direction * (filled * 0.5f)).ShouldSpawnMotesAt(map)) { return; }

            var angle = direction.AngleFlat();
            var perpendicular = new Vector3(direction.z, 0f, -direction.x);
            for (int i = 0; i < 2; i++)
            {
                var pos = start + direction * Rand.Range(0f, filled) + perpendicular * Rand.Range(-halfWidth, halfWidth) * 0.85f;
                var def = i == 0 ? VVFleckDefOf.VV_Fleck_ApocryphaAimSpark : VVFleckDefOf.VV_Fleck_ApocryphaAimTwinkle;
                var data = FleckMaker.GetDataStatic(pos, map, def, i == 0 ? Rand.Range(0.25f, 0.45f) : Rand.Range(0.2f, 0.35f));
                data.velocityAngle = angle + Rand.Range(-20f, 20f);
                data.velocitySpeed = Rand.Range(0.5f, 1.3f);
                data.rotation = Rand.Range(0f, 360f);
                data.rotationRate = Rand.Range(-120f, 120f);
                map.flecks.CreateFleck(data);
            }
        }

        public static void RegisterTrail(CompTrailRenderer trail, Vector3 point)
        {
            point.y = AltitudeLayer.MoteOverhead.AltitudeFor() - 0.01f;
            trail?.RegisterNewTrail(point);
        }

        // 한 틱에 여러 칸을 날아가도 궤적이 끊기지 않도록 촉이 지나친 구간에 일정 간격으로 몸통 위치를 찍는다.
        public static void RegisterTrailAlong(CompTrailRenderer trail, Vector3 start, Vector3 direction, float fromTip, float toTip)
        {
            for (var d = fromTip + TrailPointSpacing; d < toTip; d += TrailPointSpacing)
            {
                RegisterTrail(trail, BodyOnLane(start, direction, d));
            }

            RegisterTrail(trail, BodyOnLane(start, direction, toTip));
        }

        public static void PlayEffectAt(Map map, EffecterDef effecterDef, Vector3 point)
        {
            var cell = point.ToIntVec3().ClampInsideMap(map);
            ApocryphaGimmicks.PlayEffect(effecterDef, cell, map, OffsetFromCell(point, cell));
        }

        private static Vector3 OffsetFromCell(Vector3 point, IntVec3 cell)
        {
            return (point - cell.ToVector3Shifted()).WithY(0f);
        }
        #endregion
    }
}
