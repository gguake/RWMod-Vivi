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
                var yieldStat = thing.GetStatValue(targetYieldStat, cacheStaleAfterTicks: 2000);
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

        // 채집 산출의 무작위 반올림: 소수부 확률로 하나 더한다. 소수부가 0이면 난수를 쓰지 않는다(JobDriver_Gather·Filth_Pollen.GatherPollen과 같은 식).
        public static int RollCount(float count) => (int)count + (Rand.Chance(count - (int)count) ? 1 : 0);
    }
}
