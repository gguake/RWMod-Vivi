using RimWorld;
using Verse;

namespace VVRace
{
    public class PawnRelationWorker_MindLink : PawnRelationWorker
    {
        public override void Notify_PostRemovedByDeath(Pawn firstPawn, Pawn secondPawn)
        {
            if (firstPawn.TryGetMindTransmitter(out var firstMindTransmitter))
            {
                firstMindTransmitter.UnassignPawnControl(secondPawn, false);
            }
            else if (secondPawn.TryGetMindTransmitter(out var secondMindTransmitter))
            {
                secondMindTransmitter.UnassignPawnControl(firstPawn, false);
            }
        }
    }
}
