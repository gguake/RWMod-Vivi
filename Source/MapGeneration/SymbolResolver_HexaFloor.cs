using RimWorld;
using RimWorld.BaseGen;
using System.Linq;
using UnityEngine;
using Verse;

namespace VVRace
{
    public class SymbolResolver_HexaFloor : SymbolResolver
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
            var map = BaseGen.globalSettings.map;
            var terrainGrid = map.terrainGrid;
            var terrainDef = resolveParams.floorDef ?? BaseGenUtility.RandomBasicFloorDef(resolveParams.faction);
            bool floorOnlyIfTerrainSupports = resolveParams.floorOnlyIfTerrainSupports ?? false;
            bool allowBridgeOnAnyImpassableTerrain = resolveParams.allowBridgeOnAnyImpassableTerrain ?? false;

            foreach (var cell in HexagonalRoomUtility.GetHexagonalFills(resolveParams.rect).Select(v => v.ToIntVec3))
            {
                if (!floorOnlyIfTerrainSupports || GenConstruct.CanBuildOnTerrain(terrainDef, cell, map, Rot4.North) || (allowBridgeOnAnyImpassableTerrain && cell.GetTerrain(map).passability == Traversability.Impassable))
                {
                    terrainGrid.SetTerrain(cell, terrainDef);

                    if (resolveParams.filthDef != null)
                    {
                        FilthMaker.TryMakeFilth(cell, map, resolveParams.filthDef, !resolveParams.filthDensity.HasValue ? 1 : Mathf.RoundToInt(resolveParams.filthDensity.Value.RandomInRange));
                    }
                }
            }
        }
    }
}
