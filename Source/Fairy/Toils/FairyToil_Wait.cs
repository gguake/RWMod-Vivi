using UnityEngine;
using Verse;

namespace VVRace
{
    public class FairyToil_Wait : FairyToil
    {
        private int ticksLeft;
        private FairyState state;
        private bool drivesState;
        private bool playPhaseEffect;
        private bool enterIdleOnComplete;

        public FairyToil_Wait() { }

        public FairyToil_Wait(int ticks)
        {
            ticksLeft = ticks;
        }

        public FairyToil_Wait(int ticks, FairyState state, bool playPhaseEffect, bool enterIdleOnComplete)
        {
            ticksLeft = ticks;
            this.state = state;
            drivesState = true;
            this.playPhaseEffect = playPhaseEffect;
            this.enterIdleOnComplete = enterIdleOnComplete;
        }

        protected override void OnStarted()
        {
            if (drivesState)
            {
                Fairy?.StartTimedState(state, ticksLeft, playPhaseEffect);
            }
        }

        protected override FairyToilStatus TickAction(int delta)
        {
            ticksLeft -= delta;
            if (drivesState)
            {
                Fairy?.SetTimedStateTicks(Mathf.Max(0, ticksLeft));
            }

            if (ticksLeft > 0)
            {
                return FairyToilStatus.Running;
            }

            if (enterIdleOnComplete)
            {
                Fairy?.EnterIdle();
            }
            return FairyToilStatus.Complete;
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref ticksLeft, "ticksLeft");
            Scribe_Values.Look(ref state, "state");
            Scribe_Values.Look(ref drivesState, "drivesState");
            Scribe_Values.Look(ref playPhaseEffect, "playPhaseEffect");
            Scribe_Values.Look(ref enterIdleOnComplete, "enterIdleOnComplete");
        }
    }
}
