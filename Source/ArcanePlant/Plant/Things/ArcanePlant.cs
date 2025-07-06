using LudeonTK;
using RimWorld;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;

namespace VVRace
{
    public interface INotifyBuildingDeconstruct
    {
        void Notify_BuildingDeconstruct(ThingOwner<Thing> leavingThingOwner);
    }

    [StaticConstructorOnStartup]
    public class ArcanePlant : Building, INotifyBuildingDeconstruct
    {
        public const int RequiredTicksForRecoveryFromDamaged = 30000;

        private static List<ThingDef> _allArcanePlantDefs;
        public static List<ThingDef> AllArcanePlantDefs
        {
            get
            {
                if (_allArcanePlantDefs == null)
                {
                    _allArcanePlantDefs = DefDatabase<ThingDef>.AllDefsListForReading
                        .Where(def => typeof(ArcanePlant).IsAssignableFrom(def.thingClass))
                        .ToList();
                }

                return _allArcanePlantDefs;
            }
        }

        public CompMana ManaComp
        {
            get
            {
                if (_cachedManaComp == null)
                {
                    _cachedManaComp = GetComp<CompMana>();
                }
                return _cachedManaComp;
            }
        }
        private CompMana _cachedManaComp;

        public ArcaneSeedExtension SeedExtension
        {
            get
            {
                if (_seedExtension == null)
                {
                    _seedExtension = def.GetModExtension<ArcaneSeedExtension>();
                }
                return _seedExtension;
            }
        }
        private ArcaneSeedExtension _seedExtension;

        protected virtual bool ShouldFlip => thingIDNumber % 2 == 0;

        protected virtual bool HasRandomDrawScale => true;

        private bool _forceMinify = false;

        private int _lastDamagedTick = 0;

        public ArcanePlant()
        {
        }

        public override void ExposeData()
        {
            base.ExposeData();

            Scribe_Values.Look(ref _forceMinify, "forceMinify");
            Scribe_Values.Look(ref _lastDamagedTick, "lastDamagedTick");
        }

        public override void Print(SectionLayer layer)
        {
            try
            {
                {
                    Rand.PushState();
                    Rand.Seed = base.thingIDNumber.GetHashCode();

                    var thingTrueCenter = this.TrueCenter();

                    var drawSize = def.graphicData.drawSize;

                    bool isShift = false;
                    var zero = thingTrueCenter + new Vector3(0f, 0f, 0.11f);
                    if (zero.z - 0.5f < Position.z)
                    {
                        zero.z = Position.z + 0.5f;
                        isShift = true;
                    }

                    var scale = HasRandomDrawScale ? Rand.Range(0.9f, 1.1f) : 1f;
                    drawSize.Scale(new Vector2(scale, scale));

                    var isFlipUV = ShouldFlip;
                    var material = Graphic.MatSingleFor(this);
                    Graphic.TryGetTextureAtlasReplacementInfo(material, def.category.ToAtlasGroup(), isFlipUV, vertexColors: false, out material, out var uvs, out var _);

                    var colors = new Color32[4];
                    colors[1].a = (colors[2].a = 25);
                    colors[0].a = (colors[3].a = 0);

                    Printer_Plane.PrintPlane(
                        size: drawSize,
                        layer: layer,
                        center: zero,
                        mat: material,
                        rot: 0f,
                        flipUv: isFlipUV,
                        uvs: uvs,
                        colors: colors,
                        uvzPayload: this.HashOffset() % 1024);

                    if (def.graphicData.shadowData != null)
                    {
                        Vector3 center = thingTrueCenter + def.graphicData.shadowData.offset;
                        if (isShift)
                        {
                            center.z = base.Position.ToVector3Shifted().z + def.graphicData.shadowData.offset.z;
                        }

                        center.y -= 3f / 82f;
                        Vector3 volume = def.graphicData.shadowData.volume;
                        Printer_Shadow.PrintShadow(layer, center, volume, Rot4.North);
                    }
                }
            }
            finally
            {
                Rand.PopState();
            }
        }

        public override int UpdateRateTicks => 1200;
        protected override int MaxTickIntervalRate => 1000;

        protected override void TickInterval(int delta)
        {
            if (!Spawned || Destroyed)
            {
                return;
            }

            if (_forceMinify || !ArcanePlantUtility.CanPlaceArcanePlantToCell(Map, Position, def))
            {
                _forceMinify = false;
                ForceMinifyAndDropDirect();
                return;
            }

            if (HitPoints < MaxHitPoints && ManaComp.Active && GenTicks.TicksGame >= _lastDamagedTick + RequiredTicksForRecoveryFromDamaged)
            {
                if (GenTicks.TicksGame % 3 == 0)
                {
                    HitPoints++;
                }
            }
        }

        public override AcceptanceReport DeconstructibleBy(Faction faction)
        {
            if (DebugSettings.godMode)
            {
                return true;
            }

            return Spawned ? Faction == faction : true;
        }

        public override void PreApplyDamage(ref DamageInfo dinfo, out bool absorbed)
        {
            base.PreApplyDamage(ref dinfo, out absorbed);

            var damage = dinfo.Amount;
            if (dinfo.Def.Worker is DamageWorker_Blunt || dinfo.Def.isExplosive)
            {
                damage *= 0.8f;
            }

            if (damage < 1f)
            {
                absorbed = true;
            }
            else
            {
                dinfo.SetAmount(damage);
            }
        }

        public override void PostApplyDamage(DamageInfo dinfo, float totalDamageDealt)
        {
            base.PostApplyDamage(dinfo, totalDamageDealt);

            _lastDamagedTick = GenTicks.TicksGame;
        }

        public void Notify_BuildingDeconstruct(ThingOwner<Thing> leavingThingOwner)
        {
            if (leavingThingOwner == null) { return; }

            try
            {
                Rand.PushState(thingIDNumber);

                var seedCount = (int)SeedExtension.leavingSeedCountCurve.Evaluate(Rand.Value);
                if (seedCount > 0)
                {
                    var thing = ThingMaker.MakeThing(SeedExtension.seedDef);
                    thing.stackCount = seedCount;
                    leavingThingOwner.TryAdd(thing);
                }
            }
            finally
            {
                Rand.PopState();
            }
        }

        public void ReserveAutoMinify()
        {
            _forceMinify = true;
        }

        private void ForceMinifyAndDropDirect()
        {
            var position = Position;
            var map = Map;
            var minified = this.MakeMinified();
            GenPlace.TryPlaceThing(minified, position, map, ThingPlaceMode.Direct);
        }
    }
}
