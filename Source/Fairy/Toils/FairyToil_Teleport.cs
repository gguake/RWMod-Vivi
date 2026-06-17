using UnityEngine;
using Verse;

namespace VVRace
{
    public class FairyToil_Teleport : FairyToil
    {
        private IntVec3 cell;
        private int ticksLeft;

        public FairyToil_Teleport() { }

        public FairyToil_Teleport(IntVec3 cell)
        {
            this.cell = cell;
            ticksLeft = ViviFairy.TeleportDurationTicks;
        }

        protected override void OnStarted()
        {
            Fairy?.TeleportTo(cell);
            if (ticksLeft <= 0)
            {
                ticksLeft = ViviFairy.TeleportDurationTicks;
            }
        }

        protected override FairyToilStatus TickAction(int delta)
        {
            ticksLeft -= delta;
            Fairy?.SetTimedStateTicks(Mathf.Max(0, ticksLeft));
            if (ticksLeft > 0)
            {
                return FairyToilStatus.Running;
            }

            Fairy?.EnterIdleFromToil();
            return FairyToilStatus.Complete;
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref cell, "cell");
            Scribe_Values.Look(ref ticksLeft, "ticksLeft", ViviFairy.TeleportDurationTicks);
        }
    }
}
