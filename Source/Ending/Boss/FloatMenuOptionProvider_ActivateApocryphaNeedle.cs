using RimWorld;
using System.Collections.Generic;
using Verse;
using Verse.AI;

namespace VVRace
{
    // 바늘은 Ethereal Thing이라 바닐라 우클릭 메뉴의 선택 대상이 아니므로 클릭 위치 주변에서 직접 찾는다.
    public class FloatMenuOptionProvider_ActivateApocryphaNeedle : FloatMenuOptionProvider
    {
        protected override bool Drafted => true;
        protected override bool Undrafted => true;
        protected override bool Multiselect => false;

        protected override FloatMenuOption GetSingleOption(FloatMenuContext context)
        {
            var pawn = context.FirstSelectedPawn;
            if (pawn == null || pawn.Faction != Faction.OfPlayer) { return null; }

            var needle = ApocryphaNeedle.FindPlantedNear(context.map, context.clickPosition);
            if (needle == null) { return null; }

            var label = LocalizeString_EndingBoss.VV_FloatMenu_ActivateApocryphaNeedle.Translate().Resolve();
            if (!needle.CanActivate)
            {
                return new FloatMenuOption($"{label} ({LocalizeString_EndingBoss.VV_FailReason_ApocryphaNeedleDormant.Translate()})", null);
            }

            if (!pawn.health.capacities.CapableOf(PawnCapacityDefOf.Moving))
            {
                return new FloatMenuOption($"{label} ({LocalizeString_EndingGimmicks.VV_FailReason_FairyFlowerCannotMove.Translate()})", null);
            }

            if (!pawn.CanReach(needle.GroundCell, PathEndMode.Touch, Danger.Deadly))
            {
                return new FloatMenuOption($"{label} ({"NoPath".Translate()})", null);
            }

            return FloatMenuUtility.DecoratePrioritizedTask(
                new FloatMenuOption(label, () =>
                {
                    var job = JobMaker.MakeJob(ApocryphaJobDefOf.VV_ActivateApocryphaNeedle, needle);
                    pawn.jobs.TryTakeOrderedJob(job, JobTag.Misc);
                }, MenuOptionPriority.High),
                pawn,
                needle);
        }
    }
}
