using RimWorld;
using System.Collections.Generic;
using Verse;

namespace VVRace
{
    public class HediffGiver_GeneticInstablity : HediffGiver
    {
        public const int CheckInterval = 6000;

        public List<HediffDef> causingHediffs;
        public float chance;

        public string letterLabel;
        public string letter;

        public override void OnIntervalPassed(Pawn pawn, Hediff cause)
        {
            if (pawn.Dead || !pawn.IsNestedHashIntervalTick(60, CheckInterval))
            {
                return;
            }

            if (!Rand.Chance(chance))
            {
                return;
            }

            var hasHediff = false;
            for (int i = 0; i < causingHediffs.Count; ++i)
            {
                if (pawn.health.hediffSet.HasHediff(causingHediffs[i]))
                {
                    hasHediff = false;
                    break;
                }
            }

            if (hasHediff && TryApply(pawn))
            {
                Find.LetterStack.ReceiveLetter(letterLabel, letter.Formatted(pawn.Named("PAWN")).AdjustedFor(pawn), LetterDefOf.NegativeEvent, pawn);
            }
        }
    }
}
