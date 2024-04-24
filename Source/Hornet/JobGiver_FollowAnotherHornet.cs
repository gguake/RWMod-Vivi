using RimWorld;
using System.Collections.Generic;
using System.Linq;
using Verse;
using Verse.AI;

namespace VVRace
{
    public class JobGiver_WanderNearAllyHornets : JobGiver_WanderAnywhere
    {
        public JobGiver_WanderNearAllyHornets()
        {
            wanderRadius = 5f;
            ticksBetweenWandersRange = new IntRange(200, 400);
            locomotionUrgency = LocomotionUrgency.Walk;
            locomotionUrgencyOutsideRadius = LocomotionUrgency.Jog;
        }

        private static List<Pawn> _tmpMapHornets = new List<Pawn>();
        protected override IntVec3 GetWanderRoot(Pawn pawn)
        {
            _tmpMapHornets.Clear();
            _tmpMapHornets.AddRange(pawn.Map.mapPawns.AllPawnsSpawned.Where(p => p.def == VVThingDefOf.VV_TitanicHornet));
            foreach (var hornet in _tmpMapHornets)
            {
                if (pawn == hornet) { continue; }

                if (hornet.IsAttacking() || hornet.Downed)
                {
                    return hornet.Position;
                }
            }

            if (_tmpMapHornets.Count > 0)
            {
                return _tmpMapHornets.RandomElement().Position;
            }

            return pawn.Position;
        }
    }
}
