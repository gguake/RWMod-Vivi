using RimWorld;
using UnityEngine;
using Verse;

namespace VVRace
{
    public class FairyToil_Teleport : FairyToil
    {
        private IntVec3 cell;
        private Thing lookTarget;
        private int ticksLeft;

        public FairyToil_Teleport() { }

        public FairyToil_Teleport(IntVec3 cell)
        {
            this.cell = cell;
            ticksLeft = ViviFairy.TeleportDurationTicks;
        }

        public FairyToil_Teleport(IntVec3 cell, Thing lookTarget) : this(cell)
        {
            this.lookTarget = lookTarget;
        }

        protected override void OnStarted()
        {
            var fairy = Fairy;
            fairy?.TeleportTo(cell);
            if (fairy != null && lookTarget != null && lookTarget.Spawned && lookTarget.Map == fairy.Map)
            {
                fairy.FaceTowards(lookTarget.TrueCenter());
            }
            if (ticksLeft <= 0)
            {
                ticksLeft = ViviFairy.TeleportDurationTicks;
            }
        }

        protected override FairyToilStatus TickAction(int delta)
        {
            ticksLeft -= delta;
            if (ticksLeft > 0)
            {
                return FairyToilStatus.Running;
            }

            Fairy?.EnterIdle();
            return FairyToilStatus.Complete;
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref cell, "cell");
            Scribe_References.Look(ref lookTarget, "lookTarget");
            Scribe_Values.Look(ref ticksLeft, "ticksLeft", ViviFairy.TeleportDurationTicks);
        }
    }
}
