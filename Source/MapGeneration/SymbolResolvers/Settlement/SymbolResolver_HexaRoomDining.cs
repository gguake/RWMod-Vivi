using RimWorld;
using RimWorld.BaseGen;
using System.Collections.Generic;
using Verse;

namespace VVRace
{
    public class SymbolResolver_HexaRoomDining : SymbolResolver
    {
        private static IList<(int xOffset, int zOffset, Rot4 rot)> _smallShelfLayout
            = new List<(int, int, Rot4)>()
            {
                (-3, 2, Rot4.East),
                (-3, -2, Rot4.East),
                (3, 2, Rot4.West),
                (3, -2, Rot4.West),
            };

        public override void Resolve(ResolveParams resolveParams)
        {
            var center = resolveParams.rect.CenterCell;

            var b = Rand.Bool;
            var pots = new List<(int xOffset, int zOffset, ThingDef plantDef)>()
            {
                (0, 4, b ? VVThingDefOf.VV_Peashooter : VVThingDefOf.VV_EmberBloom),
                (0, -4, !b ? VVThingDefOf.VV_Peashooter : VVThingDefOf.VV_EmberBloom),
            };

            #region 꽃 생성
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

            #region 화분 생성
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
                p.rect = CellRect.CenteredOn(center, 5, 5);
                p.singleThingStuff = ThingDefOf.WoodLog;
                p.faction = resolveParams.faction;
                BaseGen.symbolStack.Push("placeChairsNearTables", p);
            }
            #endregion

            #region 식탁 생성
            {
                var p = resolveParams;
                p.rect = CellRect.CenteredOn(center, 3, 3);
                p.singleThingDef = VVThingDefOf.Table3x3c;
                p.singleThingStuff = VVThingDefOf.VV_Viviwax;
                p.faction = resolveParams.faction;
                BaseGen.symbolStack.Push("thing", p);
            }
            #endregion

            #region 저장된 아이템 생성
            {
                foreach (var smallShelf in _smallShelfLayout)
                {
                    var p = resolveParams;
                    p.rect = new CellRect(center.x + smallShelf.xOffset, center.z + smallShelf.zOffset, 1, 1);
                    p.thingSetMakerDef = VVThingSetMakerDefOf.VV_SettlementDiningRoomThingSet;
                    p.faction = resolveParams.faction;
                    BaseGen.symbolStack.Push("vv_thingSet_storage", p);
                }
            }
            #endregion

            #region 선반 생성
            {
                foreach (var smallShelf in _smallShelfLayout)
                {
                    var p = resolveParams;
                    p.rect = new CellRect(center.x + smallShelf.xOffset, center.z + smallShelf.zOffset, 1, 1);
                    p.thingRot = smallShelf.rot;
                    p.singleThingDef = VVThingDefOf.ShelfSmall;
                    p.singleThingStuff = VVThingDefOf.VV_Viviwax;
                    p.faction = resolveParams.faction;
                    BaseGen.symbolStack.Push("thing", p);
                }
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
