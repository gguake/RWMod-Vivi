using Verse;
using Verse.AI.Group;

namespace VVRace
{
    public class LordJob_HornetAssault : LordJob
    {
        public override StateGraph CreateGraph()
        {
            var graph = new StateGraph();
            graph.AddToil(new LordToil_HornetAssault());
            return graph;
        }

        public override bool ShouldRemovePawn(Pawn p, PawnLostCondition reason)
        {
            if (reason == PawnLostCondition.InMentalState && p.MentalStateDef == VVMentalStateDefOf.VV_HornetBerserk)
            {
                return false;
            }

            return true;
        }
    }
}
