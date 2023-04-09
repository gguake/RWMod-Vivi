using Verse;
using Verse.AI;

namespace VVRace
{
    public class JobGiver_ManagingMindLink : ThinkNode_JobGiver
    {
        public override float GetPriority(Pawn pawn)
        {
            if (!pawn.TryGetMindTransmitter(out var mindTransmitter) || !mindTransmitter.CanAddMindLink) { return 0f; }

            return 5f;
        }

        protected override Job TryGiveJob(Pawn pawn)
        {
            if (!pawn.TryGetMindTransmitter(out var mindTransmitter) || !mindTransmitter.CanAddMindLink) { return null; }

            foreach (var candidate in pawn.Map.mapPawns.SpawnedPawnsInFaction(pawn.Faction))
            {
                if (candidate.Dead || !candidate.Spawned || !candidate.TryGetViviGene(out var vivi)) { continue; }

                if (vivi.ViviControlSettings == null && vivi.MindLinkWishPawn == pawn)
                {
                    return JobMaker.MakeJob(VVJobDefOf.VV_ConnectMindLink, candidate);
                }
                else if (vivi.ViviControlSettings != null && candidate.TryGetMindLinkMaster(out var master) && master == pawn && vivi.MindLinkUnassignRequested)
                {
                    return JobMaker.MakeJob(VVJobDefOf.VV_DisconnectMindLink, candidate);
                }
            }

            return null;
        }
    }
}
