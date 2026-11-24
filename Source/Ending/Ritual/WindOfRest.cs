using RimWorld;
using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace VVRace
{
    public class WindOfRest : Thing
    {
        private const int SpreadTicks = 240;
        private const float MinSpeed = 0.6f;
        private const int GustSamplesPerTick = 24;
        private const int MaxGustsPerTick = 6;

        private static readonly Color[] PetalColors =
        {
            new Color(1f, 0.97f, 0.86f, 0.9f),
            new Color(1f, 0.9f, 0.68f, 0.9f),
            new Color(0.92f, 1f, 0.9f, 0.85f),
        };

        private static readonly List<Pawn> _tmpPawns = new List<Pawn>();

        private Vector3 _center;
        private float _radius;
        private float _maxRadius;
        private float _speed;
        private HashSet<int> _processed = new HashSet<int>();

        private bool _anyEffect;

        [Unsaved]
        private List<IntVec3>[] _cellBuckets;
        [Unsaved]
        private int _nextBucket;

        public void Init(Vector3 center)
        {
            _center = center.Yto0();
        }

        public override void SpawnSetup(Map map, bool respawningAfterLoad)
        {
            base.SpawnSetup(map, respawningAfterLoad);
            if (!respawningAfterLoad)
            {
                if (_center == Vector3.zero)
                {
                    _center = Position.ToVector3Shifted().Yto0();
                }

                _maxRadius = EndingVfxUtility.MaxDistanceToMapCorner(map, _center) + 1f;
                _speed = Mathf.Max(MinSpeed, _maxRadius / SpreadTicks);
                _radius = 0f;

                if (WindOfRestUtility.EndToxicFallout(map))
                {
                    _anyEffect = true;
                }

                var ringDef = VVFleckDefOf.VV_Fleck_GoldenWaveRing;
                if (ringDef != null)
                {
                    for (int i = 0; i < 2; i++)
                    {
                        var data = FleckMaker.GetDataStatic(_center.WithY(AltitudeLayer.MoteOverhead.AltitudeFor()), map, ringDef, 1f + i * 0.6f);
                        data.rotation = Rand.Range(0f, 360f);
                        map.flecks.CreateFleck(data);
                    }
                }
            }

            BuildEnvironmentBuckets(map);
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref _center, "center");
            Scribe_Values.Look(ref _radius, "radius", 0f);
            Scribe_Values.Look(ref _maxRadius, "maxRadius", 0f);
            Scribe_Values.Look(ref _speed, "speed", MinSpeed);
            Scribe_Collections.Look(ref _processed, "processed", LookMode.Value);
            Scribe_Values.Look(ref _anyEffect, "anyEffect", false);

            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                _processed ??= new HashSet<int>();
            }
        }

        protected override void Tick()
        {
            base.Tick();
            if (!Spawned) { return; }

            _radius += _speed;
            CleanseEnvironmentToRadius();
            ReachPawns();
            if (!Spawned) { return; }

            ThrowGusts();

            if (_radius >= _maxRadius)
            {
                Finish();
            }
        }

        private void BuildEnvironmentBuckets(Map map)
        {
            _cellBuckets = null;
            _nextBucket = 0;
            if (map == null || _maxRadius <= 0f) { return; }

            var speed = Mathf.Max(MinSpeed, _speed);
            var bucketCount = Mathf.Max(1, Mathf.CeilToInt(_maxRadius / speed) + 1);
            var buckets = new List<IntVec3>[bucketCount];
            var any = false;

            foreach (var cell in map.AllCells)
            {
                if (!WindOfRestUtility.HasCleanableEnvironment(map, cell)) { continue; }

                var dist = (cell.ToVector3Shifted().Yto0() - _center).MagnitudeHorizontal();
                if (dist <= _radius)
                {
                    if (WindOfRestUtility.CleanseCell(map, cell))
                    {
                        _anyEffect = true;
                    }
                    continue;
                }

                var idx = Mathf.Clamp(Mathf.FloorToInt(dist / speed), 0, bucketCount - 1);
                var list = buckets[idx];
                if (list == null)
                {
                    list = new List<IntVec3>();
                    buckets[idx] = list;
                }
                list.Add(cell);
                any = true;
            }

            if (any)
            {
                _cellBuckets = buckets;
                _nextBucket = Mathf.Clamp(Mathf.FloorToInt(_radius / speed), 0, bucketCount);
            }
        }

        private void CleanseEnvironmentToRadius()
        {
            if (_cellBuckets == null) { return; }

            var map = Map;
            var speed = Mathf.Max(MinSpeed, _speed);
            var targetBucket = Mathf.Min(_cellBuckets.Length - 1, Mathf.FloorToInt(_radius / speed));
            while (_nextBucket <= targetBucket)
            {
                var list = _cellBuckets[_nextBucket];
                if (list != null)
                {
                    for (int i = 0; i < list.Count; i++)
                    {
                        if (WindOfRestUtility.CleanseCell(map, list[i]))
                        {
                            _anyEffect = true;
                        }
                    }
                    _cellBuckets[_nextBucket] = null;
                }

                _nextBucket++;
            }
        }

        private void ReachPawns()
        {
            var map = Map;
            var radiusSqr = _radius * _radius;

            _tmpPawns.Clear();
            _tmpPawns.AddRange(map.mapPawns.AllPawns);
            foreach (var pawn in _tmpPawns)
            {
                if (pawn == null || pawn.Destroyed || pawn.Dead || pawn.MapHeld != map || _processed.Contains(pawn.thingIDNumber)) { continue; }
                if (!pawn.PositionHeld.IsValid) { continue; }

                var pos = pawn.Spawned ? pawn.DrawPos : pawn.PositionHeld.ToVector3Shifted();
                if ((pos - _center).MagnitudeHorizontalSquared() > radiusSqr) { continue; }

                _processed.Add(pawn.thingIDNumber);
                try
                {
                    Reach(pawn);
                }
                catch (System.Exception ex)
                {
                    Log.Error($"[ViViRace] area purification failed on {pawn}: {ex}");
                }
            }

            _tmpPawns.Clear();
        }

        private void Reach(Pawn pawn)
        {
            if (pawn.def == VVThingDefOf.VV_TitanicHornet)
            {
                if (pawn.Spawned)
                {
                    WindOfRestUtility.DissolveHornet(pawn, WindAt(pawn.DrawPos));
                    _anyEffect = true;
                }
                return;
            }

            if (WindOfRestUtility.Cleanse(pawn))
            {
                _anyEffect = true;
                if (pawn.Spawned)
                {
                    VVEffecterDefOf.VV_Effecter_WindOfRestCleanse?.SpawnAttached(pawn, pawn.Map).Cleanup();
                }
            }
        }

        private Vector3 WindAt(Vector3 pos)
        {
            return EndingVfxUtility.SafeNormalizeHorizontal(pos - _center, Vector3.forward);
        }

        private void ThrowGusts()
        {
            var map = Map;
            if (Find.CurrentMap != map) { return; }

            var view = Find.CameraDriver.CurrentViewRect.ExpandedBy(3);
            var gusts = 0;
            for (int i = 0; i < GustSamplesPerTick && gusts < MaxGustsPerTick; i++)
            {
                var angle = Rand.Range(0f, 360f);
                var dir = Vector3Utility.FromAngleFlat(angle);
                var pos = _center + dir * (_radius + Rand.Range(-1.5f, 0.5f));
                var cell = pos.ToIntVec3();
                if (!view.Contains(cell) || !cell.InBounds(map)) { continue; }

                gusts++;
                pos.y = AltitudeLayer.MoteOverhead.AltitudeFor();

                var gustDef = VVFleckDefOf.VV_Fleck_WindOfRestGust;
                if (gustDef != null && Rand.Chance(0.6f))
                {
                    var gust = FleckMaker.GetDataStatic(pos, map, gustDef, Rand.Range(1.4f, 2.4f));
                    gust.rotation = angle;
                    gust.velocityAngle = angle;
                    gust.velocitySpeed = Rand.Range(3f, 5f);
                    map.flecks.CreateFleck(gust);
                }

                var petalDef = VVFleckDefOf.VV_Fleck_EndingPetal;
                if (petalDef != null)
                {
                    var petal = FleckMaker.GetDataStatic(pos, map, petalDef, Rand.Range(0.5f, 1f));
                    petal.rotation = Rand.Range(0f, 360f);
                    petal.rotationRate = Rand.Range(-240f, 240f);
                    petal.velocityAngle = angle + Rand.Range(-25f, 25f);
                    petal.velocitySpeed = Rand.Range(1.5f, 3.5f);
                    petal.instanceColor = PetalColors.RandomElement();
                    map.flecks.CreateFleck(petal);
                }
            }
        }

        private void Finish()
        {
            var map = Map;
            if (map != null)
            {
                if (WindOfRestUtility.CleanseRemainingEnvironment(map))
                {
                    _anyEffect = true;
                }

                map.gasGrid?.RecalculateEverHadGas();
            }

            Messages.Message(
                LocalizeString_Message.VV_Message_WindOfRestResult.Translate(),
                _anyEffect ? MessageTypeDefOf.PositiveEvent : MessageTypeDefOf.NeutralEvent);

            Destroy();
        }
    }

    public static class WindOfRestUtility
    {
        private static HashSet<HediffDef> _cleansableHediffs;

        public static bool IsCleansableHediff(HediffDef def)
        {
            return def != null && GetCleansableHediffs().Contains(def);
        }

        private static HashSet<HediffDef> GetCleansableHediffs()
        {
            if (_cleansableHediffs != null) { return _cleansableHediffs; }

            var birthdayHediffs = new HashSet<HediffDef>();
            var giverSets = DefDatabase<HediffGiverSetDef>.AllDefsListForReading;
            for (int i = 0; i < giverSets.Count; i++)
            {
                var givers = giverSets[i].hediffGivers;
                if (givers == null) { continue; }

                for (int j = 0; j < givers.Count; j++)
                {
                    if (givers[j] is HediffGiver_Birthday birthday && birthday.hediff != null)
                    {
                        birthdayHediffs.Add(birthday.hediff);
                    }
                }
            }

            var set = new HashSet<HediffDef>();
            AddIfNotNull(set, HediffDefOf.ToxicBuildup);
            AddIfNotNull(set, HediffDefOf.ToxGasExposure);
            AddIfNotNull(set, HediffDefOf.LungRotExposure);
            AddIfNotNull(set, HediffDefOf.LungRot);
            AddIfNotNull(set, HediffDefOf.Scaria);
            AddIfNotNull(set, HediffDefOf.ScariaInfection);

            var incidents = DefDatabase<IncidentDef>.AllDefsListForReading;
            for (int i = 0; i < incidents.Count; i++)
            {
                var disease = incidents[i].diseaseIncident;
                if (disease != null && !disease.chronic && !birthdayHediffs.Contains(disease))
                {
                    set.Add(disease);
                }
            }

            _cleansableHediffs = set;
            return _cleansableHediffs;
        }

        private static void AddIfNotNull(HashSet<HediffDef> set, HediffDef def)
        {
            if (def != null)
            {
                set.Add(def);
            }
        }

        public static bool Cleanse(Pawn pawn)
        {
            var hediffs = pawn?.health?.hediffSet?.hediffs;
            if (hediffs == null) { return false; }

            var cleansable = GetCleansableHediffs();
            List<Hediff> toRemove = null;
            for (int i = 0; i < hediffs.Count; i++)
            {
                var hediff = hediffs[i];
                if (hediff?.def != null && cleansable.Contains(hediff.def))
                {
                    toRemove ??= new List<Hediff>();
                    toRemove.Add(hediff);
                }
            }

            if (toRemove == null) { return false; }

            for (int i = 0; i < toRemove.Count; i++)
            {
                pawn.health.RemoveHediff(toRemove[i]);
            }

            return true;
        }

        public static bool HasCleanableEnvironment(Map map, IntVec3 cell)
        {
            if (ModsConfig.BiotechActive && map.pollutionGrid != null && map.pollutionGrid.IsPolluted(cell))
            {
                return true;
            }

            var gasGrid = map.gasGrid;
            if (gasGrid != null && (gasGrid.DensityAt(cell, GasType.ToxGas) > 0 || gasGrid.DensityAt(cell, GasType.RotStink) > 0))
            {
                return true;
            }

            return false;
        }

        public static bool CleanseCell(Map map, IntVec3 cell)
        {
            var cleaned = false;
            if (ModsConfig.BiotechActive && map.pollutionGrid != null && map.pollutionGrid.IsPolluted(cell))
            {
                cell.Unpollute(map);
                cleaned = true;
            }

            var gasGrid = map.gasGrid;
            if (gasGrid != null && (gasGrid.DensityAt(cell, GasType.ToxGas) > 0 || gasGrid.DensityAt(cell, GasType.RotStink) > 0))
            {
                var smoke = gasGrid.DensityAt(cell, GasType.BlindSmoke);
                var deadlife = gasGrid.DensityAt(cell, GasType.DeadlifeDust);
                gasGrid.SetDirect(cell, smoke, 0, 0, deadlife);
                map.mapDrawer.MapMeshDirty(cell, MapMeshFlagDefOf.Gas);
                cleaned = true;
            }

            return cleaned;
        }

        public static bool CleanseRemainingEnvironment(Map map)
        {
            var cleaned = false;
            foreach (var cell in map.AllCells)
            {
                if (CleanseCell(map, cell))
                {
                    cleaned = true;
                }
            }

            return cleaned;
        }

        public static bool EndToxicFallout(Map map)
        {
            if (GameConditionDefOf.ToxicFallout == null) { return false; }

            var ended = EndToxicFalloutIn(map?.gameConditionManager);
            if (EndToxicFalloutIn(Find.World?.gameConditionManager))
            {
                ended = true;
            }

            return ended;
        }

        private static bool EndToxicFalloutIn(GameConditionManager manager)
        {
            if (manager == null) { return false; }

            var active = manager.ActiveConditions;
            var ended = false;
            for (int i = active.Count - 1; i >= 0; i--)
            {
                var cond = active[i];
                if (cond != null && cond.def == GameConditionDefOf.ToxicFallout && !cond.Permanent && cond.conditionCauser == null)
                {
                    cond.End();
                    ended = true;
                }
            }

            return ended;
        }

        public static void DissolveHornet(Pawn hornet, Vector3 wind)
        {
            var map = hornet.Map;
            var cell = hornet.Position;

            var dissolving = (DissolvingHornet)ThingMaker.MakeThing(VVThingDefOf.VV_DissolvingHornet);
            dissolving.InitFrom(hornet, wind);

            hornet.Destroy(DestroyMode.Vanish);

            GenSpawn.Spawn(dissolving, cell, map);

            var wingCount = Rand.RangeInclusive(1, 2);
            for (int i = 0; i < wingCount; i++)
            {
                var wing = ThingMaker.MakeThing(VVThingDefOf.VV_TitanicHornetWing);
                GenPlace.TryPlaceThing(wing, cell, map, ThingPlaceMode.Near);
            }
        }
    }
}
