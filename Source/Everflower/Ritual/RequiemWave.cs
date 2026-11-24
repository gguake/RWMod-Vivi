using RimWorld;
using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace VVRace
{
    public class RequiemWave : Thing
    {
        public const int SpreadTicks = 240;
        public const float MinSpeed = 0.6f;

        private const int FleckSamplesPerTick = 20;
        private const int MaxFlecksPerTick = 5;

        private static readonly Color[] WispColors =
        {
            new Color(1f, 0.98f, 0.90f, 0.9f),
            new Color(1f, 0.92f, 0.72f, 0.9f),
            new Color(0.88f, 0.95f, 1f, 0.85f),
        };

        private static readonly List<Thing> _tmpCorpses = new List<Thing>();

        private Vector3 _center;
        private float _radius;
        private float _maxRadius;
        private float _speed;
        private int _dissolvedCount;

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

            _maxRadius = EndingVfxUtility.MaxDistanceToMapCorner(map, _center) + 1f;
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
            Scribe_Values.Look(ref _dissolvedCount, "dissolvedCount", 0);
        }

        protected override void Tick()
        {
            base.Tick();
            if (!Spawned) { return; }

            _radius += _speed;
            ReachCorpses();
            if (!Spawned) { return; }

            ThrowWaveFlecks();

            if (_radius >= _maxRadius)
            {
                Finish();
            }
        }

        private void ReachCorpses()
        {
            var map = Map;
            var radiusSqr = _radius * _radius;

            _tmpCorpses.Clear();
            _tmpCorpses.AddRange(map.listerThings.ThingsInGroup(ThingRequestGroup.Corpse));
            for (int i = 0; i < _tmpCorpses.Count; i++)
            {
                if (!(_tmpCorpses[i] is Corpse corpse) || !RequiemUtility.IsDissolvableCorpse(corpse)) { continue; }
                if ((corpse.DrawPos - _center).MagnitudeHorizontalSquared() > radiusSqr) { continue; }

                try
                {
                    Dissolve(corpse, map);
                }
                catch (System.Exception ex)
                {
                    Log.Error($"[ViViRace] requiem failed on {corpse}: {ex}");
                }
            }

            _tmpCorpses.Clear();
        }

        private void Dissolve(Corpse corpse, Map map)
        {
            var cell = corpse.Position;
            Find.Selector.Deselect(corpse);
            VVEffecterDefOf.VV_Effecter_RequiemCorpse?.Spawn(cell, map).Cleanup();
            corpse.Destroy(DestroyMode.Vanish);
            _dissolvedCount++;
        }

        private void ThrowWaveFlecks()
        {
            var map = Map;
            if (Find.CurrentMap != map) { return; }

            var view = Find.CameraDriver.CurrentViewRect.ExpandedBy(3);
            var spawned = 0;
            for (int i = 0; i < FleckSamplesPerTick && spawned < MaxFlecksPerTick; i++)
            {
                var angle = Rand.Range(0f, 360f);
                var dir = Vector3Utility.FromAngleFlat(angle);
                var pos = _center + dir * (_radius + Rand.Range(-1.2f, 0.5f));
                var cell = pos.ToIntVec3();
                if (!view.Contains(cell) || !cell.InBounds(map)) { continue; }

                spawned++;
                pos.y = AltitudeLayer.MoteOverhead.AltitudeFor();

                var petalDef = VVFleckDefOf.VV_Fleck_EndingPetal;
                if (petalDef != null)
                {
                    var petal = FleckMaker.GetDataStatic(pos, map, petalDef, Rand.Range(0.45f, 0.9f));
                    petal.rotation = Rand.Range(0f, 360f);
                    petal.rotationRate = Rand.Range(-180f, 180f);
                    petal.velocityAngle = Rand.Range(-30f, 30f);
                    petal.velocitySpeed = Rand.Range(1.2f, 2.6f);
                    petal.instanceColor = WispColors.RandomElement();
                    map.flecks.CreateFleck(petal);
                }
            }
        }

        private void Finish()
        {
            if (_dissolvedCount > 0)
            {
                Messages.Message(
                    LocalizeString_Message.VV_Message_RequiemResult.Translate(_dissolvedCount.Named("COUNT")),
                    MessageTypeDefOf.PositiveEvent);
            }
            else
            {
                Messages.Message(
                    LocalizeString_Message.VV_Message_RequiemResultNone.Translate(),
                    MessageTypeDefOf.NeutralEvent);
            }

            Destroy();
        }
    }

    public static class RequiemUtility
    {
        public static bool IsDissolvableCorpse(Corpse corpse)
        {
            if (corpse == null || corpse.Destroyed || !corpse.Spawned || corpse.Bugged) { return false; }
            if (corpse is UnnaturalCorpse) { return false; }

            var inner = corpse.InnerPawn;
            if (inner == null || !inner.RaceProps.Humanlike) { return false; }
            if (inner.Faction == Faction.OfPlayer || inner.IsColonist || inner.IsSlaveOfColony) { return false; }

            return true;
        }
    }
}
