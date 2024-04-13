using RimWorld;
using RimWorld.BaseGen;
using Verse;

namespace VVRace
{
    public class SymbolResolver_HexaEmptyRoom : SymbolResolver
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
            if (!resolveParams.noRoof.HasValue || !resolveParams.noRoof.Value)
            {
                BaseGen.symbolStack.Push("vv_hexa_roof", resolveParams);
            }

            {
                var p = resolveParams;
                p.faction = resolveParams.faction;
                BaseGen.symbolStack.Push("vv_hexa_edge_honeycomb_wall", p);
            }

            {
                var p = resolveParams;
                p.floorDef = resolveParams.floorDef;
                BaseGen.symbolStack.Push("vv_hexa_floor", p);
            }

            {
                BaseGen.symbolStack.Push("vv_hexa_clear", resolveParams);
            }

            if (resolveParams.addRoomCenterToRootsToUnfog.HasValue && resolveParams.addRoomCenterToRootsToUnfog.Value && Current.ProgramState == ProgramState.MapInitializing)
            {
                MapGenerator.rootsToUnfog.Add(resolveParams.rect.CenterCell);
            }
        }

        private Thing TrySpawnWall(IntVec3 c, ResolveParams resolveParams)
        {
            var map = BaseGen.globalSettings.map;
            var terrainGrid = map.terrainGrid;
            var thingList = c.GetThingList(map);
            for (int i = 0; i < thingList.Count; i++)
            {
                if (!thingList[i].def.destroyable)
                {
                    return null;
                }
                if (thingList[i] is Building_Door)
                {
                    return null;
                }
            }

            for (int num = thingList.Count - 1; num >= 0; num--)
            {
                thingList[num].Destroy();
            }

            var terrainDef = resolveParams.floorDef;
            if (terrainDef != null)
            {
                bool floorOnlyIfTerrainSupports = resolveParams.floorOnlyIfTerrainSupports ?? false;
                bool allowBridgeOnAnyImpassableTerrain = resolveParams.allowBridgeOnAnyImpassableTerrain ?? false;

                if (!floorOnlyIfTerrainSupports || GenConstruct.CanBuildOnTerrain(terrainDef, c, map, Rot4.North) || (allowBridgeOnAnyImpassableTerrain && c.GetTerrain(map).passability == Traversability.Impassable))
                {
                    terrainGrid.SetTerrain(c, terrainDef);
                }
            }

            var wall = ThingMaker.MakeThing(VVThingDefOf.VV_ViviHoneycombWall);
            wall.SetFaction(resolveParams.faction);
            return GenSpawn.Spawn(wall, c, map);
        }
    }
}
