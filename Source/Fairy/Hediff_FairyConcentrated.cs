using RimWorld;
using Verse;

namespace VVRace
{

    public class Hediff_FairyConcentrated : HediffWithComps
    {
        public const int DurationTicks = 2000;

        public Pawn ownerVivi;
        private int ticksLeft = DurationTicks;

        public override bool ShouldRemove
        {
            get
            {
                if (ticksLeft <= 0) { return true; }
                if (ownerVivi == null || ownerVivi.Dead || !ownerVivi.Spawned) { return true; }
                if (pawn == null || pawn.DeadOrDowned || !pawn.Spawned) { return true; }
                if (pawn.Map != ownerVivi.Map) { return true; }
                if (!pawn.HostileTo(ownerVivi)) { return true; }
                return false;
            }
        }

        public void ResetDuration()
        {
            ticksLeft = DurationTicks;
        }

        public bool IsOwnedBy(Pawn owner)
        {
            return owner != null && ownerVivi == owner && !ShouldRemove;
        }

        public override void Tick()
        {
            base.Tick();
            if (ticksLeft > 0)
            {
                ticksLeft--;
            }
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_References.Look(ref ownerVivi, "ownerVivi");
            Scribe_Values.Look(ref ticksLeft, "ticksLeft", DurationTicks);
        }
    }
}
