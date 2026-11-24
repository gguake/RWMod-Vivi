using RimWorld;
using System.Collections.Generic;
using Verse;

namespace VVRace
{
    public class Hediff_EverflowerQueen : HediffWithComps
    {
        private static readonly HashSet<Pawn> _queens = new HashSet<Pawn>();

        public static void ResetRegistry()
        {
            _queens.Clear();
        }

        internal static void Notify_HediffSetDirty(Pawn pawn)
        {
            if (pawn == null || _queens.Count == 0 || !_queens.Contains(pawn)) { return; }

            var hediffs = pawn.health?.hediffSet?.hediffs;
            if (hediffs != null)
            {
                for (int i = 0; i < hediffs.Count; i++)
                {
                    if (hediffs[i] is Hediff_EverflowerQueen)
                    {
                        return;
                    }
                }
            }

            _queens.Remove(pawn);
        }

        public static bool HasAnyQueen => _queens.Count > 0;

        public ArcanePlant_Everflower everflower;

        public override void PostAdd(DamageInfo? dinfo)
        {
            base.PostAdd(dinfo);
            if (pawn != null) { _queens.Add(pawn); }
        }

        public override void PostRemoved()
        {
            base.PostRemoved();
            if (pawn != null)
            {
                bool stillHasQueen = false;
                var hediffs = pawn.health?.hediffSet?.hediffs;
                if (hediffs != null)
                {
                    for (int i = 0; i < hediffs.Count; i++)
                    {
                        if (hediffs[i] is Hediff_EverflowerQueen && hediffs[i] != this)
                        {
                            stillHasQueen = true;
                            break;
                        }
                    }
                }
                if (!stillHasQueen)
                {
                    _queens.Remove(pawn);
                }
            }
        }

        public static bool TryInterceptDeath(Pawn pawn, DamageInfo? dinfo)
        {
            if (pawn == null || pawn.Dead || pawn.Destroyed) { return false; }

            if (EverflowerPawnHolder.IsRestoringHealth(pawn)) { return true; }

            if (!HasAnyQueen || !_queens.Contains(pawn)) { return false; }

            if (dinfo.HasValue && dinfo.Value.Def == DamageDefOf.ExecutionCut) { return false; }

            var hediff = pawn.health?.hediffSet?.GetFirstHediffOfDef(VVHediffDefOf.VV_EverflowerQueen) as Hediff_EverflowerQueen;
            var everflower = hediff?.everflower;
            if (everflower == null || everflower.Destroyed) { return false; }

            return everflower.PawnHolder.TryEnshrineInsteadOfDeath(pawn);
        }

        public static bool IsAwaitingEnshrinement(Pawn pawn)
        {
            if (pawn == null || !HasAnyQueen || !_queens.Contains(pawn)) { return false; }

            var hediff = pawn.health?.hediffSet?.GetFirstHediffOfDef(VVHediffDefOf.VV_EverflowerQueen) as Hediff_EverflowerQueen;
            var everflower = hediff?.everflower;
            return everflower != null && !everflower.Destroyed && everflower.PawnHolder.IsAwaitingEnshrinement(pawn);
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_References.Look(ref everflower, "everflower");

            if (Scribe.mode == LoadSaveMode.PostLoadInit && pawn != null)
            {
                _queens.Add(pawn);
            }
        }
    }
}
