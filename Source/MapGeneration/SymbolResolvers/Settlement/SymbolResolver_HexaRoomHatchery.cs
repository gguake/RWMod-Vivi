using RimWorld;
using RimWorld.BaseGen;
using System.Collections.Generic;
using Verse;

namespace VVRace
{
    public class SymbolResolver_HexaRoomHatchery : SymbolResolver
    {
        private static IList<(int xOffset, int zOffset, Rot4 rot)> _cribLayout
            = new List<(int, int, Rot4)>()
            {
                (-3, 2, Rot4.East),
                (-2, 3, Rot4.East),
                (3, 2, Rot4.West),
                (2, 3, Rot4.West),
            };

        private static IList<(int xOffset, int zOffset)> _hatcheryLayout
            = new List<(int, int)>()
            {
                (3, -2),
                (-3, -2),
                (-2, -3),
                (2, -3),
            };

        public override void Resolve(ResolveParams resolveParams)
        {
            var center = resolveParams.rect.CenterCell;

            var pots = new List<(int xOffset, int zOffset, ThingDef plantDef)>()
            {
                (0, 0, VVThingDefOf.VV_Richflower),
                (1, 0, VVThingDefOf.VV_EmberBloom),
                (-1, 0, VVThingDefOf.VV_EmberBloom),
                (0, -4, VVThingDefOf.VV_Waterdrops),
            };

            #region 고대 꽃 생성
            {
                foreach (var pot in pots)
                {
                    var p = resolveParams;
                    p.rect = new CellRect(center.x + pot.xOffset, center.z + pot.zOffset, 1, 1);
                    p.singleThingDef = pot.plantDef;
                    p.faction = resolveParams.faction;
                    BaseGen.symbolStack.Push("vv_arcane_plant", p);
                }
            }
            #endregion

            #region 고대 화분 생성
            {
                foreach (var pot in pots)
                {
                    var p = resolveParams;
                    p.rect = new CellRect(center.x + pot.xOffset, center.z + pot.zOffset, 1, 1);
                    p.singleThingDef = VVThingDefOf.VV_ArcanePlantPot;
                    p.singleThingStuff = VVThingDefOf.VV_Viviwax;
                    p.faction = resolveParams.faction;
                    BaseGen.symbolStack.Push("thing", p);
                }
            }
            #endregion

            #region 의자 생성
            {
                var p = resolveParams;
                p.rect = new CellRect(center.x, center.z + 2, 1, 1);
                p.thingRot = Rot4.North;
                p.singleThingDef = ThingDefOf.DiningChair;
                p.singleThingStuff = ThingDefOf.WoodLog;
                p.faction = resolveParams.faction;
                BaseGen.symbolStack.Push("thing", p);
            }
            #endregion

            #region 부화장 생성
            foreach (var hatchery in _hatcheryLayout)
            {
                var p = resolveParams;
                p.rect = new CellRect(center.x + hatchery.xOffset, center.z + hatchery.zOffset, 1, 1);
                p.singleThingDef = VVThingDefOf.VV_ViviHatchery;
                p.faction = resolveParams.faction;
                BaseGen.symbolStack.Push("thing", p);
            }
            #endregion

            #region 장난감 상자 생성
            {
                var p = resolveParams;
                p.rect = new CellRect(center.x, center.z + 4, 1, 1);
                p.singleThingDef = ThingDefOf.ToyBox;
                p.faction = resolveParams.faction;
                BaseGen.symbolStack.Push("thing", p);
            }
            #endregion

            #region 정제 작업대 생성
            foreach (var crib in _cribLayout)
            {
                var p = resolveParams;
                p.rect = new CellRect(center.x + crib.xOffset, center.z + crib.zOffset, 1, 1);
                p.thingRot = crib.rot;
                p.singleThingDef = VVThingDefOf.Crib;
                p.singleThingStuff = VVThingDefOf.VV_Viviwax;
                p.faction = resolveParams.faction;
                BaseGen.symbolStack.Push("thing", p);
            }
            #endregion

            #region 방 생성
            {
                var p = resolveParams;
                p.floorDef = resolveParams.floorDef;
                p.faction = resolveParams.faction;
                BaseGen.symbolStack.Push("vv_hexa_empty_room", p);
            }
            #endregion
        }
    }
}
