using UnityEngine;
using Verse;
using Verse.Noise;

namespace VVRace
{
    public class FairyJob_Dematerialize : FairyJob
    {
        private int ticksLeft = ViviFairy.DematerializeDurationTicks;
        private bool applyAssimilation = true;

        public override FairyJobKind Kind => FairyJobKind.Dematerialize;

        public FairyJob_Dematerialize() { }

        public FairyJob_Dematerialize(Pawn owner, bool applyAssimilation) : base(0, owner)
        {
            this.applyAssimilation = applyAssimilation;
        }

        public FairyJob_Dematerialize(Pawn owner, bool applyAssimilation, int ticksLeft) : base(0, owner)
        {
            this.applyAssimilation = applyAssimilation;
            this.ticksLeft = Mathf.Max(0, ticksLeft);
        }

        protected override bool TryGetInterruptReason(out FairyJobInterruptReason reason)
        {
            reason = FairyJobInterruptReason.None;
            return false;
        }

        protected override void MakeToils()
        {
            ResetToils(new FairyToil_Wait(ticksLeft, FairyState.Dematerializing, playPhaseEffect: true, enterIdleOnComplete: false));
        }

        protected override void OnToilSequenceFinished()
        {
            fairy.Destroy();

            if (applyAssimilation)
            {
                var owner = fairy.Owner;
                if (owner == null || owner.Dead || owner.health == null) { return; }

                var hediff = owner.health.hediffSet.GetFirstHediffOfDef(VVHediffDefOf.VV_EverflowerAssimilation);
                if (hediff == null)
                {
                    hediff = owner.health.AddHediff(VVHediffDefOf.VV_EverflowerAssimilation);
                }
                else
                {
                    hediff.Severity = Mathf.Min(hediff.Severity + 1f, hediff.def.maxSeverity);
                }
            }
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref ticksLeft, "ticksLeft", ViviFairy.DematerializeDurationTicks);
            Scribe_Values.Look(ref applyAssimilation, "applyAssimilation", true);
        }
    }
}
