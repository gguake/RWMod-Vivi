using RimWorld;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;

namespace VVRace
{
    // 파면이 진행되는 동안 저장·불러오기가 가능하도록 처리한 폰을 기록해 둔다.
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

        public void Init(Vector3 center)
        {
            _center = center.Yto0();
        }

        public override void SpawnSetup(Map map, bool respawningAfterLoad)
        {
            base.SpawnSetup(map, respawningAfterLoad);
            if (respawningAfterLoad) { return; }

            if (_center == Vector3.zero)
            {
                _center = Position.ToVector3Shifted().Yto0();
            }

            var size = map.Size;
            var farX = Mathf.Max(_center.x, size.x - _center.x);
            var farZ = Mathf.Max(_center.z, size.z - _center.z);
            _maxRadius = Mathf.Sqrt(farX * farX + farZ * farZ) + 1f;
            _speed = Mathf.Max(MinSpeed, _maxRadius / SpreadTicks);
            _radius = 0f;

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

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref _center, "center");
            Scribe_Values.Look(ref _radius, "radius", 0f);
            Scribe_Values.Look(ref _maxRadius, "maxRadius", 0f);
            Scribe_Values.Look(ref _speed, "speed", MinSpeed);
            Scribe_Collections.Look(ref _processed, "processed", LookMode.Value);
            Scribe_Values.Look(ref _anyEffect, "anyEffect", false);
        }

        protected override void Tick()
        {
            base.Tick();
            if (!Spawned) { return; }

            _radius += _speed;
            ReachPawns();
            if (!Spawned) { return; }

            ThrowGusts();

            if (_radius >= _maxRadius)
            {
                Finish();
            }
        }

        private void ReachPawns()
        {
            var map = Map;
            var radiusSqr = _radius * _radius;

            _tmpPawns.Clear();
            _tmpPawns.AddRange(map.mapPawns.AllPawnsSpawned);
            foreach (var pawn in _tmpPawns)
            {
                if (pawn.Destroyed || !pawn.Spawned || _processed.Contains(pawn.thingIDNumber)) { continue; }
                if ((pawn.DrawPos - _center).MagnitudeHorizontalSquared() > radiusSqr) { continue; }

                _processed.Add(pawn.thingIDNumber);
                try
                {
                    Reach(pawn);
                }
                catch (System.Exception ex)
                {
                    Log.Error($"[ViViRace] wind of rest failed on {pawn}: {ex}");
                }
            }

            _tmpPawns.Clear();
        }

        private void Reach(Pawn pawn)
        {
            if (pawn.def == VVThingDefOf.VV_TitanicHornet)
            {
                WindOfRestUtility.DissolveHornet(pawn, WindAt(pawn.DrawPos));
                _anyEffect = true;
                return;
            }

            if (WindOfRestUtility.Cleanse(pawn))
            {
                _anyEffect = true;
                VVEffecterDefOf.VV_Effecter_WindOfRestCleanse?.SpawnAttached(pawn, pawn.Map).Cleanup();
            }
        }

        private Vector3 WindAt(Vector3 pos)
        {
            var away = (pos - _center).Yto0();
            return away.sqrMagnitude > 0.01f ? away.normalized : Vector3.forward;
        }

        // 맵 전체 파면에 모두 뿌리면 플렉이 너무 많으므로 파면 위의 임의 지점 중 화면 안에 든 곳에만 뿌린다.
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
            // 문구는 하나, 정화한 대상이 있었는지는 메시지 종류로만 구분한다.
            Messages.Message(
                LocalizeString_Message.VV_Message_WindOfRestResult.Translate(),
                _anyEffect ? MessageTypeDefOf.PositiveEvent : MessageTypeDefOf.NeutralEvent);

            Destroy();
        }
    }

    public static class WindOfRestUtility
    {
        public static bool Cleanse(Pawn pawn)
        {
            var removedInstability = RemoveAll(pawn, VVHediffDefOf.VV_GeneticUnstability);
            var removedScaria = RemoveAll(pawn, HediffDefOf.Scaria);
            return removedInstability || removedScaria;
        }

        private static bool RemoveAll(Pawn pawn, HediffDef def)
        {
            var hediffs = pawn.health?.hediffSet?.hediffs;
            if (def == null || hediffs == null) { return false; }

            var removed = false;
            foreach (var hediff in hediffs.Where(v => v.def == def).ToList())
            {
                pawn.health.RemoveHediff(hediff);
                removed = true;
            }

            return removed;
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
