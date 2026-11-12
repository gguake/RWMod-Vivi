using System.Collections.Generic;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace VVRace
{
    public class LordToil_ApocryphaFlowerGuard : LordToil
    {
        private const int FeedIntervalTicks = 2500;
        private const int ReactionCheckIntervalTicks = 30;
        private const int GuardCheckIntervalTicks = 60;

        private static readonly List<Pawn> _tmpReleased = new List<Pawn>();

        private LordJob_ApocryphaFlowerGuard GuardJob => lord.LordJob as LordJob_ApocryphaFlowerGuard;

        public override bool AllowSatisfyLongNeeds => false;

        public override void UpdateAllDuties()
        {
            var guardJob = GuardJob;
            foreach (var pawn in lord.ownedPawns)
            {
                var flower = guardJob?.GetGuardedFlower(pawn);
                if (flower != null)
                {
                    pawn.mindState.duty = new PawnDuty(VVDutyDefOf.VV_TitanicHornetFlowerGuard, flower, guardJob.GuardRadius);
                }
                else
                {
                    pawn.mindState.duty = new PawnDuty(VVDutyDefOf.VV_TitanicHornetAssault);
                }
            }
        }

        public override void LordToilTick()
        {
            base.LordToilTick();

            var guardJob = GuardJob;
            for (int i = lord.ownedPawns.Count - 1; i >= 0; i--)
            {
                var pawn = lord.ownedPawns[i];
                if (pawn.IsHashIntervalTick(GuardCheckIntervalTicks) && guardJob?.GetGuardedFlower(pawn) == null)
                {
                    _tmpReleased.Add(pawn);
                    continue;
                }

                if (pawn.IsHashIntervalTick(ReactionCheckIntervalTicks))
                {
                    LordToil_ApocryphaEscort.TryEngageImmediately(pawn);
                }
            }

            if (_tmpReleased.Count > 0)
            {
                ApocryphaUtility.JoinHornetAssault(lord.Map, _tmpReleased);
                _tmpReleased.Clear();
            }

            LordToil_ApocryphaEscort.KeepFed(lord, FeedIntervalTicks);
        }
    }
}
