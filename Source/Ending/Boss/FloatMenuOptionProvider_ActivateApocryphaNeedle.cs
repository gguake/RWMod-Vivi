using RimWorld;
using System.Collections.Generic;
using Verse;
using Verse.AI;

namespace VVRace
{
    // 2페이즈 바닥에 꽂힌 거대 엘텍스 바늘 깨우기(우클릭 메뉴). 비비가 아니어도, 징집 여부와 관계없이 지시할 수 있다.
    // 바늘은 공격 대상이 아닌 Thing(Ethereal)이라 바닐라 우클릭 메뉴가 고르지 않으므로, 우클릭 위치 가까이 꽂힌 바늘을 직접 찾는다.
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
