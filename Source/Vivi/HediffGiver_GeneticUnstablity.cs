using RimWorld;
using System.Collections.Generic;
using Verse;

namespace VVRace
{
    public class HediffGiver_GeneticUnstablity : HediffGiver
    {
        public const int CheckInterval = 6000;

        public List<HediffDef> causingHediffs;
        public float chance;

        public string letterLabel;
        public string letter;

        public override void OnIntervalPassed(Pawn pawn, Hediff cause)
        {
            // 〈영원꽃의 축복〉을 받은 비비는 유전자 불안정에 걸리지 않는다.
            if (pawn.Dead || !pawn.IsNestedHashIntervalTick(60, CheckInterval) || EverflowerBlessingUtility.IsBlessed(pawn))
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
                    hasHediff = true;
                    break;
                }
            }

            if (hasHediff && TryApply(pawn))
            {
                pawn.GetCompVivi()?.ResetGeneInheritanceGenerations();
                Find.LetterStack.ReceiveLetter(letterLabel, letter.Formatted(pawn.Named("PAWN")).AdjustedFor(pawn), LetterDefOf.NegativeEvent, pawn);
            }
        }
    }
}
