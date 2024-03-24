using RimWorld.BaseGen;
using System.Collections.Generic;
using Verse;

namespace VVRace
{
    public class SymbolResolver_HexaRoomCrafting : SymbolResolver
    {
        private static IList<(int xOffset, int zOffset, Rot4 rot)> _shelfLayout
            = new List<(int, int, Rot4)>()
            {
                (-3, 1, Rot4.East),
                (-3, -2, Rot4.East),
                (3, 1, Rot4.West),
                (3, -2, Rot4.West),
            };

        public override void Resolve(ResolveParams resolveParams)
        {
            var center = resolveParams.rect.CenterCell;

            var pots = new List<(int xOffset, int zOffset, ThingDef plantDef)>()
            {
                (0, 0, VVThingDefOf.VV_Waterdrops),
                (0, 4, VVThingDefOf.VV_EmberBloom),
                (0, -4, VVThingDefOf.VV_EmberBloom),
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
                        p.thingSetMakerDef = VVThingSetMakerDefOf.VV_SettlementCraftingRoomThingSet;
                        p.faction = resolveParams.faction;
                        BaseGen.symbolStack.Push("vv_thingSet_storage", p);
                    }
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
            }
            #endregion

            #region 작업대 생성
            {
                var p = resolveParams;
                p.rect = new CellRect(center.x -1, center.z + 2, 3, 1);
                p.thingRot = Rot4.North;
                p.singleThingDef = VVThingDefOf.HandTailoringBench;
                p.singleThingStuff = VVThingDefOf.VV_Viviwax;
                p.faction = resolveParams.faction;
                BaseGen.symbolStack.Push("thing", p);

                p.rect = new CellRect(center.x - 1, center.z - 2, 3, 1);
                p.thingRot = Rot4.South;
                p.singleThingDef = VVThingDefOf.FueledSmithy;
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
