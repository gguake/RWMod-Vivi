using RimWorld;
using Verse;
using Verse.AI;

namespace VVRace
{
    public class JobDriver_ViviNestBuildRoof : JobDriver_AffectRoof
    {
        protected override PathEndMode PathEndMode => PathEndMode.Touch;

        protected override void DoEffect()
        {
            var nest = pawn.GetNestLordJob();
            if (nest == null) { return; }

            var map = Map;
            for (int i = 0; i < 9; ++i)
            {
                var cell = Cell + GenAdj.AdjacentCellsAndInside[i];
                if (cell.InBounds(map) && !cell.Roofed(map) && nest.Territory.IsRoomCell(cell) && ViviNestPlanner.CanRoofNow(cell, map))
                {
                    map.roofGrid.SetRoof(cell, RoofDefOf.RoofConstructed);
                    MoteMaker.PlaceTempRoof(cell, map);
                }
            }
        }

        protected override bool DoWorkFailOn() => Cell.Roofed(Map);
    }
}
