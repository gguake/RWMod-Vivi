using RimWorld.BaseGen;
using Verse;

namespace VVRace
{
    public class SymbolResolver_EdgeHoneycombWall : SymbolResolver
    {
        public override void Resolve(ResolveParams resolveParams)
        {
            var map = BaseGen.globalSettings.map;

            var edgeCells = resolveParams.rect.EdgeCells;
            foreach (var cell in edgeCells)
            {
                if (resolveParams.chanceToSkipWallBlock != null && Rand.Chance(resolveParams.chanceToSkipWallBlock.Value))
                {
                    continue;
                }

                var wall = ThingMaker.MakeThing(VVThingDefOf.VV_ViviHoneycombWall);
                wall.SetFaction(resolveParams.faction);

                if (resolveParams.hpPercentRange != null)
                {
                    var percent = resolveParams.hpPercentRange.Value.RandomInRange;
                    wall.HitPoints = (int)(wall.MaxHitPoints * percent);
                }

                GenSpawn.Spawn(wall, cell, map);
            }
        }
    }
}
