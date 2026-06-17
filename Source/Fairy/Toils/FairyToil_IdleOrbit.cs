using Verse;

namespace VVRace
{
    public class FairyToil_IdleOrbit : FairyToil
    {
        private Pawn centerPawn;
        private int slot;
        private int count;

        public FairyToil_IdleOrbit() { }

        public FairyToil_IdleOrbit(Pawn centerPawn, int slot, int count)
        {
            Configure(centerPawn, slot, count);
        }

        public void Configure(Pawn centerPawn, int slot, int count)
        {
            this.centerPawn = centerPawn;
            this.slot = slot;
            this.count = count;
        }

        protected override FairyToilStatus TickAction(int delta)
        {
            FairyJobUtility.IdleOrbitAround(Fairy, centerPawn, slot, count);
            return FairyToilStatus.Running;
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_References.Look(ref centerPawn, "centerPawn");
            Scribe_Values.Look(ref slot, "slot");
            Scribe_Values.Look(ref count, "count");
        }
    }
}
