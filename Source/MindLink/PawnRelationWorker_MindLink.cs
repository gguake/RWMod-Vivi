using RimWorld;
using System;
using Verse;

namespace VVRace
{
    public class PawnRelationWorker_MindLink : PawnRelationWorker
    {
        public override void OnRelationCreated(Pawn firstPawn, Pawn secondPawn)
        {
            base.OnRelationCreated(firstPawn, secondPawn);

            var isFirstPawnHasMindTransmitter = firstPawn.TryGetMindTransmitter(out var firstMindTransmitter);
            var isSecondPawnHasMindTransmitter = secondPawn.TryGetMindTransmitter(out var secondMindTransmitter);
            if (isFirstPawnHasMindTransmitter && isSecondPawnHasMindTransmitter)
            {
                throw new InvalidOperationException($"can't make mind link between mind linkers; {firstPawn} <-> {secondPawn}");
            }

            if (isFirstPawnHasMindTransmitter)
            {
                firstMindTransmitter.AssignPawnControl(secondPawn);
                if (secondPawn.TryGetViviGene(out var vivi))
                {
                    vivi.Notify_MakeNewMindLink();
                }
            }
            if (isSecondPawnHasMindTransmitter)
            {
                secondMindTransmitter.AssignPawnControl(firstPawn);
                if (firstPawn.TryGetViviGene(out var vivi))
                {
                    vivi.Notify_MakeNewMindLink();
                }
            }
        }

        public override void OnRelationRemoved(Pawn firstPawn, Pawn secondPawn)
        {
            base.OnRelationRemoved(firstPawn, secondPawn);

            if (firstPawn.TryGetMindTransmitter(out var firstMindTransmitter))
            {
                firstMindTransmitter.UnassignPawnControl(secondPawn);
                if (secondPawn.TryGetViviGene(out var vivi))
                {
                    vivi.Notify_RemoveMindLink();
                }
            }

            if (secondPawn.TryGetMindTransmitter(out var secondMindTransmitter))
            {
                secondMindTransmitter.UnassignPawnControl(firstPawn);
                if (secondPawn.TryGetViviGene(out var vivi))
                {
                    vivi.Notify_RemoveMindLink();
                }
            }
        }

        public override void Notify_PostRemovedByDeath(Pawn firstPawn, Pawn secondPawn)
        {
            Pawn pawn = (firstPawn.HasMindTransmitter() ? firstPawn : secondPawn);
            Pawn pawn2 = ((firstPawn == pawn) ? secondPawn : firstPawn);

            if (!pawn2.Dead && pawn2.TryGetViviGene(out var vivi))
            {
                vivi.Notify_ForceBreakingMindLink();
            }
        }
    }
}
