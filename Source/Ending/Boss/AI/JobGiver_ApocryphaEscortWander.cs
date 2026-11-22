using RimWorld;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace VVRace
{
    public class JobGiver_ApocryphaEscortWander : JobGiver_WanderNearAllyHornets
    {
        protected override IntVec3 GetWanderRoot(Pawn pawn)
        {
            var mates = pawn.GetLord()?.ownedPawns;
            if (mates != null)
            {
                for (int i = 0; i < mates.Count; i++)
                {
                    var mate = mates[i];
                    if (mate != pawn && mate.Spawned && (mate.IsAttacking() || mate.Downed))
                    {
                        return mate.Position;
                    }
                }
            }

            var escortee = pawn.mindState.duty?.focus.Thing;
            return escortee != null && escortee.Spawned ? escortee.Position : pawn.Position;
        }
    }
}
