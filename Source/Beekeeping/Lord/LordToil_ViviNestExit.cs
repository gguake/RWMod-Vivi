using RimWorld;
using System.Collections.Generic;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace VVRace
{
    public class LordToil_ViviNestExit : LordToil_ViviNest
    {
        private const int LeftBehindCheckInterval = 60;

        private static readonly List<Pawn> _tmpLeftBehind = new List<Pawn>();

        private readonly LocomotionUrgency _locomotion;
        private readonly bool _canDig;

        public override bool AllowSatisfyLongNeeds => false;

        public LordToil_ViviNestExit(LocomotionUrgency locomotion, bool canDig)
        {
            _locomotion = locomotion;
            _canDig = canDig;
        }

        public override void UpdateAllDuties()
        {
            for (int i = 0; i < lord.ownedPawns.Count; ++i)
            {
                var duty = new PawnDuty(VVDutyDefOf.VV_ViviNestExit)
                {
                    locomotion = _locomotion,
                    canDig = _canDig,
                };
                lord.ownedPawns[i].mindState.duty = duty;
            }
        }

        public override void LordToilTick()
        {
            base.LordToilTick();
            if (Find.TickManager.TicksGame % LeftBehindCheckInterval != 0) { return; }

            var noCarrier = !AnyFreeCarrier();
            var pawns = lord.ownedPawns;
            for (int i = 0; i < pawns.Count; ++i)
            {
                if (ShouldLeaveBehind(pawns[i], noCarrier))
                {
                    _tmpLeftBehind.Add(pawns[i]);
                }
            }
            for (int i = 0; i < _tmpLeftBehind.Count; ++i)
            {
                var pawn = _tmpLeftBehind[i];
                lord.Notify_PawnLost(pawn, !pawn.DevelopmentalStage.Baby() && pawn.Downed ? PawnLostCondition.Incapped : PawnLostCondition.Undefined);
            }
            _tmpLeftBehind.Clear();
        }

        private bool ShouldLeaveBehind(Pawn pawn, bool noCarrier)
        {
            if (!pawn.Spawned && !IsCarriedByMember(pawn)) { return true; }
            return pawn.DevelopmentalStage.Baby() ? noCarrier && pawn.Spawned && !IsBeingFetched(pawn) : pawn.Downed;
        }

        private bool IsCarriedByMember(Pawn pawn)
        {
            var carrier = pawn.CarriedBy;
            return carrier != null && carrier.GetLord() == lord;
        }

        private bool AnyFreeCarrier()
        {
            var pawns = lord.ownedPawns;
            for (int i = 0; i < pawns.Count; ++i)
            {
                var pawn = pawns[i];
                if (pawn.Spawned && !pawn.Downed && pawn.DevelopmentalStage.Adult() && pawn.carryTracker?.CarriedThing == null && pawn.CurJobDef != JobDefOf.Kidnap)
                {
                    return true;
                }
            }
            return false;
        }

        private bool IsBeingFetched(Pawn baby)
        {
            var pawns = lord.ownedPawns;
            for (int i = 0; i < pawns.Count; ++i)
            {
                var job = pawns[i].CurJob;
                if (job != null && job.def == JobDefOf.Kidnap && job.targetA.Thing == baby) { return true; }
            }
            return false;
        }
    }
}
