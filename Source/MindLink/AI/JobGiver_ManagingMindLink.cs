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
                if (candidate.Dead || candidate.Downed || !candidate.IsColonistPlayerControlled || !candidate.TryGetViviGene(out var vivi) || vivi.ViviMindLinkSettings == null) { continue; }

                if (vivi.ViviMindLinkSettings.HediffMindLink == null && vivi.ViviMindLinkSettings?.ReservedToConnectTarget == pawn)
                {
                    return JobMaker.MakeJob(VVJobDefOf.VV_ConnectMindLink, candidate);
                }
                else if (vivi.ViviMindLinkSettings?.ReservedToDisconnect == true && candidate.TryGetMindLinkMaster(out var master) && master == pawn)
                {
                    return JobMaker.MakeJob(VVJobDefOf.VV_DisconnectMindLink, candidate);
                }
            }

            return null;
        }
    }
}
