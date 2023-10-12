using RimWorld;
using RimWorld.BaseGen;
using System.Collections.Generic;
using Verse;

namespace VVRace
{
    public class SymbolResolver_HexaRoomRefining : SymbolResolver
    {
        private static IList<(int xOffset, int zOffset, Rot4 rot)> _gatheringTableLayout
            = new List<(int, int, Rot4)>()
            {
                (-3, 1, Rot4.East),
                (3, 1, Rot4.West),
            };

        private static IList<(int xOffset, int zOffset, Rot4 rot)> _shelfLayout
            = new List<(int, int, Rot4)>()
            {
                (-3, -2, Rot4.East),
                (3, -2, Rot4.West),
            };

        private static IList<(int xOffset, int zOffset, Rot4 rot)> _smallShelfLayout
            = new List<(int, int, Rot4)>()
            {
                (0, 4, Rot4.South),
                (0, -4, Rot4.North),
            };

        public override void Resolve(ResolveParams resolveParams)
        {
            var center = resolveParams.rect.CenterCell;

            var artificialPlantPot = new List<(int xOffset, int zOffset, ThingDef plantDef)>()
            {
                (0, 0, VVThingDefOf.VV_Richflower),
                (0, 1, VVThingDefOf.VV_EmberBloom),
                (1, 0, VVThingDefOf.VV_Peashooter),
                (-1, 0, VVThingDefOf.VV_Peashooter),
                (0, -1, VVThingDefOf.VV_Peashooter),
            };

            #region 고대 꽃 생성
            {
                foreach (var pot in artificialPlantPot)
                {
                    var p = resolveParams;
                    p.rect = new CellRect(center.x + pot.xOffset, center.z + pot.zOffset, 1, 1);
                    p.singleThingDef = pot.plantDef;
                    p.faction = resolveParams.faction;
                    BaseGen.symbolStack.Push("vv_artificial_plant", p);
                }
            }
            #endregion

            #region 고대 화분 생성
            {
                foreach (var pot in artificialPlantPot)
                {
                    var p = resolveParams;
                    p.rect = new CellRect(center.x + pot.xOffset, center.z + pot.zOffset, 1, 1);
                    p.singleThingDef = VVThingDefOf.VV_AncientPlantPot;
                    p.singleThingStuff = VVThingDefOf.VV_Viviwax;
                    p.faction = resolveParams.faction;
                    BaseGen.symbolStack.Push("thing", p);
                }
            }
            #endregion

            #region 저장된 아이템 생성
            {
                foreach (var shelf in _shelfLayout)
                {
                    foreach (var cell in new CellRect(center.x + shelf.xOffset, center.z + shelf.zOffset, 1, 2))
                    {
                        var p = resolveParams;
                        p.rect = new CellRect(cell.x, cell.z, 1, 1);
                        p.thingSetMakerDef = VVThingSetMakerDefOf.VV_SettlementRefiningRoomThingSet;
                        p.faction = resolveParams.faction;
                        BaseGen.symbolStack.Push("vv_thingSet_storage", p);
                    }
                }

                foreach (var smallShelf in _smallShelfLayout)
                {
                    var p = resolveParams;
                    p.rect = new CellRect(center.x + smallShelf.xOffset, center.z + smallShelf.zOffset, 1, 1);
                    p.thingSetMakerDef = VVThingSetMakerDefOf.VV_SettlementRefiningRoomThingSet;
                    p.faction = resolveParams.faction;
                    BaseGen.symbolStack.Push("vv_thingSet_storage", p);
                }
            }
            #endregion

            #region 선반 생성
            {
                foreach (var shelf in _shelfLayout)
                {
                    var p = resolveParams;
                    p.rect = new CellRect(center.x + shelf.xOffset, center.z + shelf.zOffset, 1, 2);
                    p.thingRot = shelf.rot;
                    p.singleThingDef = VVThingDefOf.Shelf;
                    p.singleThingStuff = VVThingDefOf.VV_Viviwax;
                    p.faction = resolveParams.faction;
                    BaseGen.symbolStack.Push("thing", p);
                }

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

            #region 정제 작업대 생성
            foreach (var table in _gatheringTableLayout)
            {
                var p = resolveParams;
                p.rect = new CellRect(center.x + table.xOffset, center.z + table.zOffset, 1, 2);
                p.thingRot = table.rot;
                p.singleThingDef = VVThingDefOf.VV_RefiningWorkbench;
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
