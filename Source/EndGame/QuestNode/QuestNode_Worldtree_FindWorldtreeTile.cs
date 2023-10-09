using RimWorld.Planet;
using RimWorld.QuestGen;
using Verse;

namespace VVRace
{
    public class QuestNode_Worldtree_FindWorldtreeTile : QuestNode
    {
        private const int MinTraversalDistance = 90;

        private const int MaxTraversalDistance = 400;

        [NoTranslate]
        public SlateRef<string> storeAs;

        private bool TryFindRootTile(out int tile)
        {
            int tile2;
            return TileFinder.TryFindRandomPlayerTile(out tile, allowCaravans: false, (int x) => TryFindDestinationTileActual(x, MinTraversalDistance, out tile2));
        }

        private bool TryFindDestinationTile(int rootTile, out int tile)
        {
            int distance = MaxTraversalDistance;
            for (int i = 0; i < 1000; i++)
            {
                distance = (int)(distance * Rand.Range(0.5f, 0.75f));
                if (distance <= MinTraversalDistance)
                {
                    distance = MinTraversalDistance;
                }
                if (TryFindDestinationTileActual(rootTile, distance, out tile))
                {
                    return true;
                }
                if (distance <= MinTraversalDistance)
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
                bool canTraverseImpassable = i == 1;
                if (TileFinder.TryFindPassableTileWithTraversalDistance(
                    rootTile, 
                    minDist, 
                    MaxTraversalDistance, 
                    out tile, 
                    (int x) => !Find.WorldObjects.AnyWorldObjectAt(x) && Find.WorldGrid[x].biome.canBuildBase && Find.WorldGrid[x].biome.canAutoChoose && Find.WorldGrid[x].hilliness == Hilliness.Flat, 
                    ignoreFirstTilePassability: true, 
                    TileFinderMode.Near, 
                    canTraverseImpassable))
                {
                    return true;
                }
            }
            tile = -1;
            return false;
        }

        protected override void RunInt()
        {
            Slate slate = QuestGen.slate;
            TryFindRootTile(out var tile);
            TryFindDestinationTile(tile, out var tile2);
            slate.Set(storeAs.GetValue(slate), tile2);
        }

        protected override bool TestRunInt(Slate slate)
        {
            int tile2;
            if (TryFindRootTile(out var tile))
            {
                return TryFindDestinationTile(tile, out tile2);
            }
            return false;
        }
    }
}
