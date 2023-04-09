using RimWorld;
using Verse;

namespace VVRace
{
    public class Hediff_MindLink : HediffWithComps
    {
        public override bool ShouldRemove => pawn.GetMindLinkMaster() == null;

        public override void PostMake()
        {
            base.PostMake();
            UpdateSeverity();
        }

        public override void PostTick()
        {
            base.PostTick();

            if (pawn.IsHashIntervalTick(2500))
            {
                UpdateSeverity();
            }
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
