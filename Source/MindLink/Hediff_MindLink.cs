using RimWorld;
using Verse;

namespace VVRace
{
    public class Hediff_MindLink : HediffWithComps
    {
        public Pawn linker;
        public int connectStartTicks;
        public bool disconnectReserved;

        /// <summary> MindLink가 유지된 총 틱 </summary>
        public int ConnectedTicks => GenTicks.TicksGame - connectStartTicks;

        public override bool ShouldRemove => pawn.GetMindLinkMaster() == null;

        public override void ExposeData()
        {
            base.ExposeData();

            Scribe_References.Look(ref linker, "linker");
            Scribe_Values.Look(ref connectStartTicks, "connectStartTicks");
            Scribe_Values.Look(ref disconnectReserved, "disconnectReserved");
        }

        public override void PostMake()
        {
            base.PostMake();

            connectStartTicks = GenTicks.TicksGame;
            UpdateSeverity();

            if (pawn.TryGetViviGene(out var vivi))
            {
                vivi.ViviMindLinkSettings?.Notify_MindLinkCreated();
            }
        }

        public override void PostRemoved()
        {
            base.PostRemoved();

            if (linker.TryGetMindTransmitter(out var mindTransmitter))
            {
                mindTransmitter.UnassignPawnControl(pawn, false);
            }

            if (pawn.TryGetViviGene(out var vivi))
            {
                vivi.ViviMindLinkSettings?.Notify_MindLinkRemoved();
            }
        }

        public override void PostTick()
        {
            base.PostTick();
            if (linker == null)
            {
                pawn.health.RemoveHediff(this);
            }

            if (pawn.IsHashIntervalTick(2500))
            {
                UpdateSeverity();
            }
        }

        public override void Notify_PawnDied()
        {
            base.Notify_PawnDied();

            if (linker != null && linker.TryGetMindTransmitter(out var mindTransmitter))
            {
                mindTransmitter.UnassignPawnControl(pawn, directlyRemoveHediff: false);
            }

            pawn.health.RemoveHediff(this);
        }

        private void UpdateSeverity()
        {
            if (pawn.TryGetMindLinkMaster(out var master))
            {
                severityInt = master.health?.capacities?.GetLevel(PawnCapacityDefOf.Consciousness) ?? 0f;
            }
        }
    }
}
