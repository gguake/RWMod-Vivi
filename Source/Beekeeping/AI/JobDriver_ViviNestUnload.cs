using RimWorld;
using System.Collections.Generic;
using Verse;
using Verse.AI;

namespace VVRace
{
    public class JobDriver_ViviNestUnload : JobDriver
    {
        private const int UnloadTicks = 30;

        private IntVec3 Cell => job.GetTarget(TargetIndex.A).Cell;
        private Thing InventoryThing => job.GetTarget(TargetIndex.B).Thing;

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            return pawn.Reserve(Cell, job, errorOnFailed: errorOnFailed);
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOn(() => InventoryThing == null || !pawn.inventory.innerContainer.Contains(InventoryThing));

            yield return Toils_Goto.GotoCell(TargetIndex.A, PathEndMode.OnCell);
            yield return Toils_General.Wait(UnloadTicks);
            // 고른 보관 칸에 그대로 놓는다(둘레에 놓으면 근처 플레이어 더미에 합쳐질 수 있다). 칸이 바뀌어 못 놓으면 지닌 채로 두고 다시 고른다.
            yield return Toils_General.Do(() =>
            {
                var thing = InventoryThing;
                var count = Mathf.Min(job.count, thing.stackCount);
                if (count > 0 && pawn.inventory.innerContainer.TryDrop(thing, Cell, pawn.Map, ThingPlaceMode.Direct, count, out var dropped))
                {
                    pawn.GetNestLordJob()?.MarkDropped(dropped);
                }
            });
        }
    }
}
