using RimWorld.Planet;
using RimWorld.QuestGen;
using Verse;

namespace VVRace
{
    public class QuestNode_EndGame_DreamumVictory_FindRuinsTile : QuestNode
    {
        private const int MinTraversalDistance = 30;

        private const int MaxTraversalDistance = 180;

        [NoTranslate]
        public SlateRef<string> storeAs;

        protected override void RunInt()
        {
            var slate = QuestGen.slate;
            TryFindRootTile(out var tile);
            TryFindDestinationTile(tile, out var tile2);

            slate.Set(storeAs.GetValue(slate), tile2);
        }

        protected override bool TestRunInt(Slate slate)
        {
            if (!Find.Storyteller.difficulty.allowViolentQuests)
            {
                return false;
            }

            if (TryFindRootTile(out var tile))
            {
                return TryFindDestinationTile(tile, out _);
            }

            return false;
        }

        private bool TryFindRootTile(out int tile)
        {
            return TileFinder.TryFindRandomPlayerTile(
                out tile, 
                false, 
                t => TryFindDestinationTileActual(t, MinTraversalDistance, out _));
        }

        private bool TryFindDestinationTile(int rootTile, out int tile)
        {
            int maxDistance = MaxTraversalDistance;
            for (int i = 0; i < 1000; i++)
            {
                maxDistance = (int)((float)maxDistance * Rand.Range(0.25f, 0.5f));
                if (maxDistance <= MinTraversalDistance)
                {
                    maxDistance = MinTraversalDistance;
                }
                if (TryFindDestinationTileActual(rootTile, maxDistance, out tile))
                {
                    return true;
                }
                if (maxDistance <= MinTraversalDistance)
                {
                    return false;
                }
            }

            tile = -1;
            return false;
        }

        private bool TryFindDestinationTileActual(int rootTile, int minDist, out int tile)
        {
            for (int i = 0; i < 2; i++)
            {
                if (TileFinder.TryFindPassableTileWithTraversalDistance(
                    rootTile, 
                    minDist, 
                    MaxTraversalDistance, 
                    out tile, 
                    t => !Find.WorldObjects.AnyWorldObjectAt(t) && Find.WorldGrid[t].biome.canBuildBase && Find.WorldGrid[t].biome.canAutoChoose && Find.WorldGrid[t].hilliness != Hilliness.Mountainous, 
                    true, 
                    TileFinderMode.Near,
                    i == 1))
                {
                    return true;
                }
            }

            tile = -1;
            return false;
        }
    }
}
