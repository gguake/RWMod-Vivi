using Verse;

namespace VVRace
{
    public static class ViviRaceUtility
    {
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
