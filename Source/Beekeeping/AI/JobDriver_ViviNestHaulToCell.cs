using System.Collections.Generic;
using Verse;
using Verse.AI;

namespace VVRace
{
    // 바닐라 운반을 그대로 쓰되, 내려놓은 물건은 플레이어가 자동으로 가져가지 않게 금지 표시한다.
    public class JobDriver_ViviNestHaulToCell : JobDriver_HaulToCell
    {
        protected override IEnumerable<Toil> MakeNewToils()
        {
            var haulDef = job.GetTarget(TargetIndex.A).Thing?.def;
            AddFinishAction(condition =>
            {
                if (condition != JobCondition.Succeeded || haulDef == null || pawn.Map == null) { return; }

                var cell = job.GetTarget(TargetIndex.B).Cell;
                foreach (var thing in cell.GetThingList(pawn.Map))
                {
                    if (thing.def == haulDef)
                    {
                        pawn.GetNestLordJob()?.MarkOwned(thing);
                    }
                }
            });

            foreach (var toil in base.MakeNewToils())
            {
                yield return toil;
            }
        }
    }
}
