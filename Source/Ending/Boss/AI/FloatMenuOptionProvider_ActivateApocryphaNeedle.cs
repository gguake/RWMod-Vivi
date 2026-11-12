using RimWorld;
using Verse;
using Verse.AI;

namespace VVRace
{
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

            var label = LocalizeString_Etc.VV_FloatMenu_ActivateApocryphaNeedle.Translate().Resolve();
            if (!needle.CanActivate)
            {
                return new FloatMenuOption($"{label} ({LocalizeString_Etc.VV_FailReason_ApocryphaNeedleDormant.Translate()})", null);
            }

            if (!pawn.health.capacities.CapableOf(PawnCapacityDefOf.Moving))
            {
                return new FloatMenuOption($"{label} ({LocalizeString_Etc.VV_FailReason_CannotMove.Translate()})", null);
            }

            if (!pawn.CanReach(needle.GroundCell, PathEndMode.Touch, Danger.Deadly))
            {
                return new FloatMenuOption($"{label} ({"NoPath".Translate()})", null);
            }

            return FloatMenuUtility.DecoratePrioritizedTask(
                new FloatMenuOption(label, () =>
                {
                    var job = JobMaker.MakeJob(VVJobDefOf.VV_ActivateApocryphaNeedle, needle);
                    pawn.jobs.TryTakeOrderedJob(job, JobTag.Misc);
                }, MenuOptionPriority.High),
                pawn,
                needle);
        }
    }
}
