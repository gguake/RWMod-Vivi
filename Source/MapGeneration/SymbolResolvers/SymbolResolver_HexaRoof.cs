using RimWorld;
using RimWorld.BaseGen;
using System.Linq;

namespace VVRace
{

    public class SymbolResolver_HexaRoof : SymbolResolver
    {
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
            if (resolveParams.noRoof.HasValue && resolveParams.noRoof.Value)
            {
                return;
            }

            var roofGrid = BaseGen.globalSettings.map.roofGrid;
            var def = resolveParams.roofDef ?? RoofDefOf.RoofConstructed;

            foreach (var cell in HexagonalRoomUtility.GetHexagonalFills(resolveParams.rect).Select(v => v.ToIntVec3))
            {
                if (!roofGrid.Roofed(cell))
                {
                    roofGrid.SetRoof(cell, def);
                }
            }
        }
    }
}
