using RimWorld;
using RimWorld.BaseGen;
using System.Collections.Generic;
using Verse;

namespace VVRace
{
    public class SymbolResolver_HexaRoomGreenhouse : SymbolResolver
    {
        private static IList<(int xOffset, int zOffset, Rot4 rot)> _smallShelfLayout
            = new List<(int, int, Rot4)>()
            {
                (0, 4, Rot4.South),
                (0, -4, Rot4.North),
            };

        private static IList<(int xOffset, int zOffset)> _germinatorLayout
            = new List<(int, int)>()
            {
                (-3, -2),
                (-3, 1),
                (2, -2),
                (2, 1),
            };

        public override void Resolve(ResolveParams resolveParams)
        {
            var center = resolveParams.rect.CenterCell;

            var artificialPlantPot = new List<(int xOffset, int zOffset, ThingDef plantDef)>()
            {
                (0, 0, VVThingDefOf.VV_Waterdrops),
                (0, 1, VVThingDefOf.VV_EmberBloom),
                (0, -1, VVThingDefOf.VV_Peashooter),
                (0, -2, VVThingDefOf.VV_Peashooter),
                (0, 2, VVThingDefOf.VV_Peashooter),
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

            #region 배양기 생성
            {
                foreach (var germinator in _germinatorLayout)
                {
                    var p = resolveParams;
                    p.rect = new CellRect(center.x + germinator.xOffset, center.z + germinator.zOffset, 1, 1);
                    p.thingRot = Rot4.South;
                    p.singleThingDef = VVThingDefOf.VV_SeedlingGerminator;
                    p.singleThingStuff = VVThingDefOf.VV_Viviwax;
                    p.faction = resolveParams.faction;
                    BaseGen.symbolStack.Push("thing", p);
                }
            }
            #endregion

            #region 저장된 아이템 생성
            {
                foreach (var smallShelf in _smallShelfLayout)
                {
                    var p = resolveParams;
                    p.rect = new CellRect(center.x + smallShelf.xOffset, center.z + smallShelf.zOffset, 1, 1);
                    p.thingSetMakerDef = VVThingSetMakerDefOf.VV_SettlementGreenHouseThingSet;
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
