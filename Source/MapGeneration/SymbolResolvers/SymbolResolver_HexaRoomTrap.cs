using RimWorld;
using RimWorld.BaseGen;
using System.Collections.Generic;
using Verse;

namespace VVRace
{
    public class SymbolResolver_HexaRoomTrap : SymbolResolver
    {
        public override void Resolve(ResolveParams resolveParams)
        {
            var center = resolveParams.rect.CenterCell;

            var artificialPlantPot = new List<(int xOffset, int zOffset, ThingDef plantDef)>()
            {
                (0, 0, VVThingDefOf.VV_Richflower),
                (1, 0, VVThingDefOf.VV_Peashooter),
                (-1, 0, VVThingDefOf.VV_Peashooter),
                (0, 1, VVThingDefOf.VV_Peashooter),
                (0, -1, VVThingDefOf.VV_Peashooter),

                (0, 4, VVThingDefOf.VV_EmberBloom),
                (0, 3, VVThingDefOf.VV_Waterdrops),
                (1, 3, VVThingDefOf.VV_Peashooter),
                (-1, 3, VVThingDefOf.VV_Peashooter),

                (0, -4, VVThingDefOf.VV_EmberBloom),
                (0, -3, VVThingDefOf.VV_Waterdrops),
                (1, -3, VVThingDefOf.VV_Peashooter),
                (-1, -3, VVThingDefOf.VV_Peashooter),

                (-3, 1, VVThingDefOf.VV_Peashooter),
                (-3, 2, VVThingDefOf.VV_Peashooter),
                (-3, -1, VVThingDefOf.VV_Peashooter),
                (-3, -2, VVThingDefOf.VV_Peashooter),

                (3, 1, VVThingDefOf.VV_Peashooter),
                (3, 2, VVThingDefOf.VV_Peashooter),
                (3, -1, VVThingDefOf.VV_Peashooter),
                (3, -2, VVThingDefOf.VV_Peashooter),
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
