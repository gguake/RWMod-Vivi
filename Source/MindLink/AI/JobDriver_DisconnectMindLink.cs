using RimWorld;
using Verse;

namespace VVRace
{
    public class JobDriver_DisconnectMindLink : JobDriver_InteractTarget
    {
        private Pawn MainTargetPawn => MainTargetInfo.Pawn;

        protected override bool CanProgressJob
        {
            get
            {
                var targetPawn = MainTargetPawn;
                if (!pawn.IsColonistPlayerControlled || !targetPawn.IsColonistPlayerControlled)
                {
                    return false;
                }

                if (!pawn.TryGetMindTransmitter(out var mindTransmitter))
                {
                    return false;
                }

                if (!targetPawn.TryGetViviGene(out var vivi) || !vivi.MindLinkUnassignRequested)
                {
                    return false;
                }

                return true;
            }
        }

        protected override float ProgressByTick => 1f;

        protected override float WorkTotal => 400f;

        protected override void OnJobCompleted()
        {
            var targetPawn = MainTargetPawn;
            if (CanProgressJob)
            {
                if (!pawn.relations.TryRemoveDirectRelation(VVPawnRelationDefOf.VV_MindLink, targetPawn))
                {
                    Log.Warning($"pawn {pawn} is tried to remove mind link with {targetPawn} but failed by some reasons.");
                }

                Messages.Message(LocalizeTexts.MessageMindLinkDisconnected.Translate(pawn.Named("LINKER"), targetPawn.Named("LINKED")),
                    new LookTargets(new Thing[] { pawn, targetPawn }),
                    MessageTypeDefOf.NeutralEvent);
            }
        }
    }
}
