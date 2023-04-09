using Verse;

namespace VVRace
{
    public static class ViviRaceUtility
    {
        public static bool HasViviGene(this Pawn pawn)
        {
            return pawn.genes?.HasGene(VVGeneDefOf.VV_ViviGene) ?? false;
        }

        public static bool TryGetViviGene(this Pawn pawn, out Gene_Vivi gene)
        {
            if (pawn == null) { gene = null; return false; }

            gene = pawn.genes?.GetGene(VVGeneDefOf.VV_ViviGene) as Gene_Vivi;
            return gene != null;
        }

        public static bool CanMakeViviCream(this Pawn pawn)
        {
            if (!pawn.HasViviGene() || (pawn.needs?.food?.Starving ?? true)) { return false; }

            return true;
        }

        public static string GetJobFailReasonForMakeViviCream(this Pawn pawn)
        {
            if (!pawn.HasViviGene()) { return LocalizeTexts.JobFailReasonNotVivi.Translate(); }
            if (pawn.needs?.food?.Starving ?? true) { return LocalizeTexts.JobFailReasonNotVivi.Translate(); }

            return null;
        }

        public static void InterruptCurrentJob(this Pawn pawn)
        {
            if (pawn.carryTracker?.CarriedThing != null)
            {
                pawn.carryTracker.TryDropCarriedThing(pawn.Position, ThingPlaceMode.Near, out var _);
            }

            pawn.jobs?.EndCurrentJob(Verse.AI.JobCondition.InterruptForced);
            pawn.jobs?.CheckForJobOverride();
        }
    }
}
