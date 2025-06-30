using RimWorld;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;

namespace VVRace
{
    [StaticConstructorOnStartup]
    public class ArcanePlant : Building
    {
        public const float ManaByFertilizer = 20f;

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

        public CompMana CompMana
        {
            get
            {
                if (_cachedCompMana == null)
                {
                    _cachedCompMana = GetComp<CompMana>();
                }
                return _cachedCompMana;
            }
        }
        private CompMana _cachedCompMana;

        protected virtual bool ShouldFlip => thingIDNumber % 2 == 0;

        protected virtual bool HasRandomDrawScale => true;

        private bool _forceMinify = false;
        private int _nextGrowNearFlowerTick = 0;

        public ArcanePlant()
        {
        }

        public override void SpawnSetup(Map map, bool respawningAfterLoad)
        {
            base.SpawnSetup(map, respawningAfterLoad);
        }

        public override void ExposeData()
        {
            base.ExposeData();

            Scribe_Values.Look(ref _forceMinify, "forceMinify");
            Scribe_Values.Look(ref _nextGrowNearFlowerTick, "nextGrowNearFlowerTick", defaultValue: 0);
        }

        public override void Print(SectionLayer layer)
        {
            try
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
                    //topVerticesAltitudeBias: 0.1f,
                    uvzPayload: this.HashOffset() % 1024);

                if (def.graphicData.shadowData != null)
                {
                    Vector3 center = thingTrueCenter + def.graphicData.shadowData.offset;
                    if (isShift)
                    {
                        center.z = base.Position.ToVector3Shifted().z + def.graphicData.shadowData.offset.z;
                    }

                    center.y -= 3f / 74f;
                    Vector3 volume = def.graphicData.shadowData.volume;
                    Printer_Shadow.PrintShadow(layer, center, volume, Rot4.North);
                }
            }
            finally
            {
                Rand.PopState();
            }
        }

        public override int UpdateRateTicks => 200;
        protected override int MaxTickIntervalRate => 200;

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
        }

        public override AcceptanceReport DeconstructibleBy(Faction faction)
        {
            if (DebugSettings.godMode)
            {
                return true;
            }

            return Spawned ? Faction == faction : true;
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
