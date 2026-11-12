using RimWorld;
using Verse;

namespace VVRace
{
    public class Hediff_EverflowerQueen : HediffWithComps
    {
        public ArcanePlant_Everflower everflower;

        public static bool TryInterceptDeath(Pawn pawn, DamageInfo? dinfo)
        {
            if (pawn == null || pawn.Dead || pawn.Destroyed) { return false; }

            if (EverflowerPawnHolder.IsRestoringHealth(pawn)) { return true; }

            if (dinfo.HasValue && dinfo.Value.Def == DamageDefOf.ExecutionCut) { return false; }

            var hediff = pawn.health?.hediffSet?.GetFirstHediffOfDef(VVHediffDefOf.VV_EverflowerQueen) as Hediff_EverflowerQueen;
            var everflower = hediff?.everflower;
            if (everflower == null || everflower.Destroyed) { return false; }

            return everflower.PawnHolder.TryEnshrineInsteadOfDeath(pawn);
        }

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
