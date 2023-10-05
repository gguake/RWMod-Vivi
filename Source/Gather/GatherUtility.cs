using RimWorld;
using UnityEngine;
using Verse;

namespace VVRace
{
    public static class GatherUtility
    {
        public static bool InGatherableRange(this Thing billGiver, Thing target)
        {
            if (billGiver.Spawned && billGiver is Building_GatherWorkTable workTable)
            {
                var radius = workTable.GatherRadius;
                if (workTable.Position.InHorDistOf(target.Position, radius))
                {
                    return true;
                }
            }

            return false;
        }

        public static bool CanGatherable(this Thing thing, StatDef targetYieldStat, StatDef gatherableCooldownStat = null)
        {
            if (thing is Plant plant && plant.Growth < thing.GetStatValue(VVStatDefOf.VV_MinGrowthPlantGatherable))
            {
                return false;
            }

            if (targetYieldStat != null)
            {
                var yieldStat = thing.GetStatValue(targetYieldStat);
                if (yieldStat <= 0f)
                {
                    return false;
                }
            }

            if (gatherableCooldownStat != null)
            {
                var comp = thing.TryGetComp<CompRepeatGatherable>();
                if (comp == null || comp.IsCooldown(gatherableCooldownStat))
                {
                    return false;
                }
            }

            return true;
        }
    }
}
