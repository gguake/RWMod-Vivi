using System.Collections.Generic;
using Verse;
using Verse.AI;

namespace VVRace
{
    public class LordToil_ApocryphaFlowerGuard : LordToil_ApocryphaHornetGuard
    {
        private const int GuardCheckIntervalTicks = 60;

        private static readonly List<Pawn> _tmpReleased = new List<Pawn>();

        private LordJob_ApocryphaFlowerGuard GuardJob => lord.LordJob as LordJob_ApocryphaFlowerGuard;

        public override void UpdateAllDuties()
        {
            var guardJob = GuardJob;
            foreach (var pawn in lord.ownedPawns)
            {
                var flower = guardJob?.GetGuardedFlower(pawn);
                pawn.mindState.duty = flower != null
                    ? new PawnDuty(VVDutyDefOf.VV_TitanicHornetFlowerGuard, flower, guardJob.GuardRadius)
                    : new PawnDuty(VVDutyDefOf.VV_TitanicHornetAssault);
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
                    TryEngageImmediately(pawn);
                }
            }

            if (_tmpReleased.Count > 0)
            {
                ApocryphaUtility.JoinHornetAssault(lord.Map, _tmpReleased);
                _tmpReleased.Clear();
            }

            KeepFed();
        }
    }
}
