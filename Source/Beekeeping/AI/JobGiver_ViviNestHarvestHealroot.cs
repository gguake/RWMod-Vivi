using RimWorld;
using System.Collections.Generic;
using Verse;
using Verse.AI;

namespace VVRace
{
    public class JobGiver_ViviNestHarvestHealroot : ThinkNode_JobGiver
    {
        private const float SearchRadius = 24f;

        private static List<ThingDef> _healrootDefs;

        private static List<ThingDef> HealrootDefs
        {
            get
            {
                if (_healrootDefs == null)
                {
                    _healrootDefs = new List<ThingDef>();
                    foreach (var def in DefDatabase<ThingDef>.AllDefsListForReading)
                    {
                        if (def.category == ThingCategory.Plant && def.plant != null && def.plant.harvestedThingDef == ThingDefOf.MedicineHerbal)
                        {
                            _healrootDefs.Add(def);
                        }
                    }
                }
                return _healrootDefs;
            }
        }

        private static LordJob_ViviNest WorkingNest(Pawn pawn) => BeekeepingUtility.OutdoorWorkingNest(pawn, WorkTypeDefOf.Growing);

        public override float GetPriority(Pawn pawn)
        {
            var nest = WorkingNest(pawn);
            if (nest == null || nest.Production.HealrootWaiting) { return 0f; }

            return nest.Production.MedicineNeed;
        }

        protected override Job TryGiveJob(Pawn pawn)
        {
            var nest = WorkingNest(pawn);
            if (nest == null) { return null; }
            var production = nest.Production;
            if (production.MedicineNeed <= 0f || production.HealrootWaiting) { return null; }

            var plant = FindHealroot(pawn, nest, out var anyTarget, out var readyTick);
            if (plant != null) { return JobMaker.MakeJob(JobDefOf.Harvest, plant); }

            if (!anyTarget) { production.Notify_NoHealroot(readyTick); }
            return null;
        }

        private static Plant FindHealroot(Pawn pawn, LordJob_ViviNest nest, out bool anyTarget, out int readyTick)
        {
            anyTarget = false;
            readyTick = -1;

            var map = pawn.Map;
            var root = nest.NestCell;
            var territory = nest.Territory;
            var traverse = TraverseParms.For(pawn, Danger.Some);
            const int maxDistance = (int)(SearchRadius * SearchRadius);
            Plant best = null;
            var bestDistance = maxDistance;
            var defs = HealrootDefs;
            for (int d = 0; d < defs.Count; ++d)
            {
                var things = map.listerThings.ThingsOfDef(defs[d]);
                for (int i = 0; i < things.Count; ++i)
                {
                    if (!(things[i] is Plant plant)) { continue; }

                    var distance = territory.ActivityDistSq(plant.Position);
                    if (distance >= maxDistance) { continue; }
                    if (!plant.HarvestableNow)
                    {
                        readyTick = BeekeepingUtility.EarlierTick(readyTick, BeekeepingUtility.HarvestReadyTick(plant, ViviNestProduction.HealrootRetryMaxTicks));
                        continue;
                    }

                    anyTarget = true;
                    if (distance >= bestDistance) { continue; }
                    if (plant.IsForbidden(pawn) || !pawn.CanReserve(plant) || !map.reachability.CanReach(root, plant, PathEndMode.Touch, traverse)) { continue; }

                    best = plant;
                    bestDistance = distance;
                }
            }
            return best;
        }
    }
}
