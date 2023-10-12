using RimWorld;
using RimWorld.BaseGen;
using System.Collections.Generic;
using Verse;

namespace VVRace
{
    public class SymbolResolver_TurretGarden : SymbolResolver
    {
        public override void Resolve(ResolveParams resolveParams)
        {
            var map = BaseGen.globalSettings.map;
            var center = resolveParams.rect.CenterCell;

            var artificialPlantPot = new List<(int xOffset, int zOffset, ThingDef plantDef)>()
            {
                (0, 0, VVThingDefOf.VV_Richflower),
                (1, 0, VVThingDefOf.VV_Pealaucnher),
                (-1, 0, VVThingDefOf.VV_Pealaucnher),
                (0, 1, VVThingDefOf.VV_Pealaucnher),
                (0, -1, VVThingDefOf.VV_Pealaucnher),

                (1, -1, VVThingDefOf.VV_Peashooter),
                (1, 1, VVThingDefOf.VV_Peashooter),
                (-1, 1, VVThingDefOf.VV_Peashooter),
                (-1, -1, VVThingDefOf.VV_Peashooter),
            };


            #region 방어벽
            {
                var p = resolveParams;
                p.rect = CellRect.CenteredOn(center, 5, 5);
                p.wallStuff = Rand.Bool ? VVThingDefOf.VV_Viviwax : ThingDefOf.WoodLog;
                p.faction = resolveParams.faction;
                BaseGen.symbolStack.Push("vv_edgebarricade", p);
            }
            #endregion

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
                    var c = new IntVec2(center.x + pot.xOffset, center.z + pot.zOffset);
                    if (map.fertilityGrid.FertilityAt(c.ToIntVec3) > 0f)
                    {
                        continue;
                    }

                    var p = resolveParams;
                    p.rect = new CellRect(center.x + pot.xOffset, center.z + pot.zOffset, 1, 1);
                    p.singleThingDef = VVThingDefOf.VV_AncientPlantPot;
                    p.singleThingStuff = VVThingDefOf.VV_Viviwax;
                    p.faction = resolveParams.faction;
                    BaseGen.symbolStack.Push("thing", p);
                }
            }
            #endregion


        }
    }
}
