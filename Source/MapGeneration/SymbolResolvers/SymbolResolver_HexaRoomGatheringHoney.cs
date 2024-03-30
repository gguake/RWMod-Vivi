using RimWorld;
using RimWorld.BaseGen;
using System.Collections.Generic;
using Verse;

namespace VVRace
{
    public class SymbolResolver_HexaRoomGatheringHoney : SymbolResolver
    {
        private static IList<(int xOffset, int zOffset, Rot4 rot)> _tableLayout
            = new List<(int, int, Rot4)>()
            {
                (0, 4, Rot4.South),
                (0, -4, Rot4.North),
            };

        private static IList<(int xOffset, int zOffset, Rot4 rot)> _smallShelfLayout
            = new List<(int, int, Rot4)>()
            {
                (-3, 2, Rot4.East),
                (-3, -2, Rot4.East),
                (3, 2, Rot4.West),
                (3, -2, Rot4.West),
            };

        private static IList<(int xOffset, int zOffset)> _flowerPotLayout
            = new List<(int, int)>()
            {
                (0, 2),
                (1, 1),
                (2, 0),
                (1, -1),
                (0, -2),
                (-1, -1),
                (-2, 0),
                (-1, 1),
            };

        public override void Resolve(ResolveParams resolveParams)
        {
            var center = resolveParams.rect.CenterCell;

            var pots = new List<(int xOffset, int zOffset, ThingDef plantDef)>()
            {
                (0, 0, VVThingDefOf.VV_Radiantflower),
                (0, 1, VVThingDefOf.VV_Richflower),
                (0, -1, VVThingDefOf.VV_Richflower),
                (1, 0, VVThingDefOf.VV_Peashooter),
                (-1, 0, VVThingDefOf.VV_Peashooter),
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

            #region 일반 화분 생성
            {
                foreach (var flowerPot in _flowerPotLayout)
                {
                    var p = resolveParams;
                    p.rect = new CellRect(center.x + flowerPot.xOffset, center.z + flowerPot.zOffset, 1, 1);
                    p.singleThingDef = VVThingDefOf.PlantPot;
                    p.singleThingStuff = ThingDefOf.WoodLog;
                    p.faction = resolveParams.faction;
                    p.postThingSpawn = (thing) =>
                    {
                        var plantGrower = thing as Building_PlantGrower;
                        if (plantGrower != null)
                        {
                            var plant = ThingMaker.MakeThing(VVThingDefOf.Plant_Daylily) as Plant;
                            if (plant != null)
                            {
                                plant.Growth = 0.7f;
                                plant.stackCount = 1;
                                GenSpawn.Spawn(plant, thing.Position, thing.Map);
                            }
                        }
                    };
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
                    p.thingSetMakerDef = VVThingSetMakerDefOf.VV_SettlementGatheringRoomThingSet;
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

            #region 채집대 생성
            foreach (var table in _tableLayout)
            {
                var p = resolveParams;
                p.rect = new CellRect(center.x + table.xOffset, center.z + table.zOffset, 1, 1);
                p.thingRot = table.rot;
                p.singleThingDef = VVThingDefOf.VV_GatheringBarrel;
                p.singleThingStuff = ThingDefOf.WoodLog;
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
