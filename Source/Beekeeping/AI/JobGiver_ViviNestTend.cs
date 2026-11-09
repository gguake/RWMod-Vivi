using RimWorld;
using Verse;
using Verse.AI;

namespace VVRace
{
    // 다친 둥지 구성원을 바닐라 치료 잡(TendPatient)으로 치료한다. 둥지 몫 약(약뿌리에서 거둔 약초 등)이 있으면 쓰고, 없으면 약 없이 치료한다.
    // 바닐라 치료 워크기버는 플레이어가 돌보는 환자·침대에 누운 환자만 보므로 쓰지 않는다(치료 잡 자체는 NPC 의사면 침대가 없어도 끝까지 치료한다).
    // 장기 계측에서 쓰러진 구성원이 치료받지 못하고 며칠 누워 있다가 감염·추위로 죽었다.
    public class JobGiver_ViviNestTend : ThinkNode_JobGiver
    {
        protected override Job TryGiveJob(Pawn pawn)
        {
            var nest = BeekeepingUtility.SettledNestOf(pawn);
            if (nest == null || pawn.WorkTypeIsDisabled(WorkTypeDefOf.Doctor)) { return null; }

            var members = nest.lord.ownedPawns;
            for (int i = 0; i < members.Count; ++i)
            {
                var patient = members[i];
                if (patient == pawn || !patient.Spawned || !patient.health.HasHediffsNeedingTend()) { continue; }

                // 걸어 다니는 환자를 쫓지 않도록 쓰러졌거나 누운 환자만 본다(바닐라도 사람 환자는 누워 있어야 치료한다).
                if (!patient.Downed && patient.GetPosture() == PawnPosture.Standing) { continue; }
                if (!pawn.CanReserveAndReach(patient, PathEndMode.ClosestTouch, Danger.Some)) { continue; }

                return JobMaker.MakeJob(JobDefOf.TendPatient, patient, FindMedicine(pawn, nest));
            }

            return null;
        }

        private static Thing FindMedicine(Pawn pawn, LordJob_ViviNest nest)
        {
            return GenClosest.ClosestThingReachable(
                pawn.Position,
                pawn.Map,
                ThingRequest.ForGroup(ThingRequestGroup.Medicine),
                PathEndMode.ClosestTouch,
                TraverseParms.For(pawn, Danger.Some),
                validator: t => nest.Stock.IsNestOwned(t) && pawn.CanReserve(t));
        }
    }
}
