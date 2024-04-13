using RimWorld;
using RimWorld.BaseGen;
using System.Collections.Generic;
using Verse;

namespace VVRace
{
    public class SymbolResolver_HexaRoomBarracks : SymbolResolver
    {
        private static IList<(int xOffset, int zOffset, Rot4 rot)> _bedLayout 
            = new List<(int, int, Rot4)>()
            {
                (-3, 1, Rot4.South),
                (3, 1, Rot4.South),
                (-3, -2, Rot4.North),
                (3, -2, Rot4.North),

                (1, 0, Rot4.North),
                (0, -1, Rot4.East),
                (-1, -1, Rot4.South),
                (-1, 1, Rot4.West),
            };

        public override void Resolve(ResolveParams resolveParams)
        {
            var center = resolveParams.rect.CenterCell;

            var pots = new List<(int xOffset, int zOffset, ThingDef plantDef)>()
            {
                (0, 0, VVThingDefOf.VV_EmberBloom),
                (0, 4, VVThingDefOf.VV_Waterdrops),
                (0, -4, VVThingDefOf.VV_Waterdrops),
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

            #region 침대 생성
            {
                foreach (var bed in _bedLayout)
                {
                    var isVertical = bed.rot == Rot4.North || bed.rot == Rot4.South;
                    var p = resolveParams;
                    p.rect = new CellRect(center.x + bed.xOffset, center.z + bed.zOffset, isVertical ? 1 : 2, isVertical ? 2 : 1);
                    p.singleThingDef = ThingDefOf.Bed;
                    p.singleThingStuff = VVThingDefOf.VV_Viviwax;
                    p.thingRot = bed.rot;
                    p.faction = resolveParams.faction;
                    BaseGen.symbolStack.Push("bed", p);
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
