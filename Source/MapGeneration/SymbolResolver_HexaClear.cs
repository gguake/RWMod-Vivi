using RimWorld.BaseGen;
using System.Collections.Generic;
using System.Linq;
using Verse;

namespace VVRace
{
    public class SymbolResolver_HexaClear : SymbolResolver
    {
        private static List<Thing> tmpThingsToDestroy = new List<Thing>();

        public override bool CanResolve(ResolveParams resolveParams)
        {
            if (!base.CanResolve(resolveParams))
            {
                return false;
            }

            var rect = resolveParams.rect;
            if (rect.Width < HexagonalRoomUtility.RoomCellSize.x || rect.Height < HexagonalRoomUtility.RoomCellSize.z)
            {
                return false;
            }

            return true;
        }

        public override void Resolve(ResolveParams resolveParams)
        {
            foreach (var cell in HexagonalRoomUtility.GetHexagonalFills(resolveParams.rect).Select(v => v.ToIntVec3))
            {
                if (resolveParams.clearEdificeOnly.HasValue && resolveParams.clearEdificeOnly.Value)
                {
                    var edifice = cell.GetEdifice(BaseGen.globalSettings.map);
                    if (edifice != null && edifice.def.destroyable)
                    {
                        edifice.Destroy();
                    }
                }
                else if (resolveParams.clearFillageOnly.HasValue && resolveParams.clearFillageOnly.Value)
                {
                    tmpThingsToDestroy.Clear();
                    tmpThingsToDestroy.AddRange(cell.GetThingList(BaseGen.globalSettings.map));

                    for (int i = 0; i < tmpThingsToDestroy.Count; i++)
                    {
                        if (tmpThingsToDestroy[i].def.destroyable && tmpThingsToDestroy[i].def.Fillage != 0)
                        {
                            tmpThingsToDestroy[i].Destroy();
                        }
                    }
                }
                else
                {
                    tmpThingsToDestroy.Clear();
                    tmpThingsToDestroy.AddRange(cell.GetThingList(BaseGen.globalSettings.map));

                    for (int i = 0; i < tmpThingsToDestroy.Count; i++)
                    {
                        if (tmpThingsToDestroy[i].def.destroyable)
                        {
                            tmpThingsToDestroy[i].Destroy();
                        }
                    }
                }
                if (resolveParams.clearRoof.HasValue && resolveParams.clearRoof.Value)
                {
                    BaseGen.globalSettings.map.roofGrid.SetRoof(cell, null);
                }
            }
        }
    }
}
