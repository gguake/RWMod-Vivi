using RimWorld;
using Verse;

namespace VVRace
{
    public class Hediff_EverflowerQueen : HediffWithComps
    {
        public ArcanePlant_Everflower everflower;

        // Pawn.Kill 직전에 불린다. true면 죽음을 취소한다.
        // 처형·안락사는 가로채지 않으므로 여제도 그 방법으로는 죽는다.
        public static bool TryInterceptDeath(Pawn pawn, DamageInfo? dinfo)
        {
            if (pawn == null || pawn.Dead || pawn.Destroyed) { return false; }

            // 회복 도중의 건강 판정이 부른 죽음은 회복이 끝난 뒤 다시 판정하므로 여기서는 넘긴다.
            if (EverflowerPawnHolder.IsRestoringHealth(pawn)) { return true; }

            if (dinfo.HasValue && dinfo.Value.Def == DamageDefOf.ExecutionCut) { return false; }

            var hediff = pawn.health?.hediffSet?.GetFirstHediffOfDef(VVHediffDefOf.VV_EverflowerQueen) as Hediff_EverflowerQueen;
            var everflower = hediff?.everflower;
            if (everflower == null || everflower.Destroyed) { return false; }

            return everflower.PawnHolder.TryEnshrineInsteadOfDeath(pawn);
        }

        // 방금 죽음을 가로채 회복시킨 여제인지(안치 대기 중).
        public static bool IsAwaitingEnshrinement(Pawn pawn)
        {
            var hediff = pawn?.health?.hediffSet?.GetFirstHediffOfDef(VVHediffDefOf.VV_EverflowerQueen) as Hediff_EverflowerQueen;
            var everflower = hediff?.everflower;
            return everflower != null && !everflower.Destroyed && everflower.PawnHolder.IsAwaitingEnshrinement(pawn);
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_References.Look(ref everflower, "everflower");
        }
    }
}
