using RimWorld;
using RimWorld.BaseGen;
using System.Linq;
using Verse;

namespace VVRace
{
    public class SymbolResolver_HexaEdgeHoneycombWall : SymbolResolver
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
            var rect = resolveParams.rect;
            foreach (var wallCell in rect.GetHexagonalEdges().Select(v => v.ToIntVec3))
            {
                if (wallCell.InBounds(BaseGen.globalSettings.map))
                {
                    if (resolveParams.chanceToSkipWallBlock != null && Rand.Chance(resolveParams.chanceToSkipWallBlock.Value))
                    {
                        continue;
                    }

                    TrySpawnWall(wallCell, resolveParams);
                }
            }

        }

        private Thing TrySpawnWall(IntVec3 c, ResolveParams resolveParams)
        {
            var map = BaseGen.globalSettings.map;
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

            var wall = ThingMaker.MakeThing(VVThingDefOf.VV_ViviHoneycombWall);
            wall.SetFaction(resolveParams.faction);

            if (resolveParams.hpPercentRange != null)
            {
                var percent = resolveParams.hpPercentRange.Value.RandomInRange;
                wall.HitPoints = (int)(wall.MaxHitPoints * percent);
            }

            return GenSpawn.Spawn(wall, c, map);
        }
    }
}
