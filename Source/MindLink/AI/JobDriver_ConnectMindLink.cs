using RimWorld;
using System;
using System.IO;
using Verse;

namespace VVRace
{
    public class JobDriver_ConnectMindLink : JobDriver_InteractTarget
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

                if (!pawn.TryGetMindTransmitter(out var mindTransmitter) || !mindTransmitter.CanAddMindLink)
                {
                    return false;
                }

                if (!targetPawn.TryGetViviGene(out var targetVivi) || targetVivi.ViviMindLinkSettings?.ReservedToConnectTarget != pawn || targetVivi.ViviMindLinkSettings == null || targetVivi.ViviMindLinkSettings.HediffMindLink != null)
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
            if (CanProgressJob && pawn.TryGetMindTransmitter(out var mindTransmitter))
            {
                mindTransmitter.AssignPawnControl(targetPawn);
            }

            if (pawn.IsColonist)
            {
                Messages.Message(LocalizeTexts.MessageMindLinkConnected.Translate(pawn.Named("LINKER"), targetPawn.Named("LINKED")),
                    new LookTargets(new Thing[] { pawn, targetPawn }),
                    MessageTypeDefOf.NeutralEvent);
            }
        }
    }
}
