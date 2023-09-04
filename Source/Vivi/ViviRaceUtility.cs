using Verse;

namespace VVRace
{
    public static class ViviRaceUtility
    {
        public static bool CanMakeViviCream(this Pawn pawn)
        {
            if (!(pawn is Vivi) || (pawn.needs?.food?.Starving ?? true)) { return false; }

            return true;
        }

        public static string GetJobFailReasonForMakeViviCream(this Pawn pawn)
        {
            if (!(pawn is Vivi)) { return LocalizeTexts.JobFailReasonNotVivi.Translate(); }
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
