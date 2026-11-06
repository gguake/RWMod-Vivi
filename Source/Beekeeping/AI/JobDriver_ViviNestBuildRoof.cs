using RimWorld;
using Verse;
using Verse.AI;

namespace VVRace
{
    // 바닐라 지붕 짓기는 플레이어 지붕 구역만 보므로, 바닐라 지붕 잡 바탕에 둥지 방의 빈 지붕 칸을 얹는 효과만 바꿔 쓴다.
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
