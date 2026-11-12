using RimWorld;
using System;
using Verse;
using Verse.AI;

namespace VVRace
{
    public class JobGiver_ViviNestFertilizePlant : ThinkNode_JobGiver
    {
        public const float LowManaPct = 0.5f;

        protected override Job TryGiveJob(Pawn pawn)
        {
            var nest = BeekeepingUtility.SettledNestOf(pawn);
            if (nest == null || pawn.WorkTypeIsDisabled(WorkTypeDefOf.Hauling)) { return null; }
            if (nest.Stock.CountOf(VVThingDefOf.VV_Fertilizer) <= 0) { return null; }

            var indoorOnly = BeekeepingUtility.OutdoorWorkPaused(pawn);
            var plant = FindPlant(pawn, nest, indoorOnly, AvailableFertilizer(pawn, nest));
            if (plant == null) { return null; }

            Predicate<Thing> validator = t => IsUsableFertilizer(t, pawn, nest, indoorOnly) && pawn.CanReserve(t);
            if (!AnyFertilizerPasses(pawn.Map, validator)) { return null; }

            var fertilizer = GenClosest.ClosestThingReachable(
                pawn.Position,
                pawn.Map,
                ThingRequest.ForDef(VVThingDefOf.VV_Fertilizer),
                PathEndMode.ClosestTouch,
                TraverseParms.For(pawn, Danger.Some),
                validator: validator);

            return fertilizer != null ? JobMaker.MakeJob(VVJobDefOf.VV_ViviNestFertilizePlant, plant, fertilizer) : null;
        }

        private static bool AnyFertilizerPasses(Map map, Predicate<Thing> validator)
        {
            var fertilizers = map.listerThings.ThingsOfDef(VVThingDefOf.VV_Fertilizer);
            for (int i = 0; i < fertilizers.Count; ++i)
            {
                if (validator(fertilizers[i])) { return true; }
            }
            return false;
        }

        public static bool IsUsableFertilizer(Thing thing, Pawn pawn, LordJob_ViviNest nest, bool indoorOnly)
        {
            return nest.Stock.IsNestOwned(thing) && (!indoorOnly || nest.Territory.IsRoomCell(thing.Position));
        }

        private static int AvailableFertilizer(Pawn pawn, LordJob_ViviNest nest)
        {
            var available = nest.Stock.CountOf(VVThingDefOf.VV_Fertilizer);
            var members = nest.lord.ownedPawns;
            for (int i = 0; i < members.Count; ++i)
            {
                var job = members[i].CurJob;
                if (members[i] != pawn && job?.def == VVJobDefOf.VV_ViviNestFertilizePlant && job.targetA.Thing is ArcanePlant plant && plant.ManaComp != null)
                {
                    available -= JobDriver_ViviNestFertilizePlant.FertilizerCountToFill(plant.ManaComp);
                }
            }
            return available;
        }

        private static ArcanePlant FindPlant(Pawn pawn, LordJob_ViviNest nest, bool indoorOnly, int availableFertilizer)
        {
            ArcanePlant indoorBest = null, outdoorBest = null;
            var reserve = nest.Production.FertilizerTarget / 2;
            var indoorBestPct = LowManaPct;
            var outdoorBestPct = LowManaPct;
            var buildings = pawn.Map.listerBuildings.allBuildingsNonColonist;
            for (int i = 0; i < buildings.Count; ++i)
            {
                if (!(buildings[i] is ArcanePlant plant) || plant.Faction != nest.Faction) { continue; }

                var mana = plant.ManaComp;
                if (mana == null || mana.Props.manaCapacity <= 0f || mana.IsUnlimited) { continue; }

                var indoor = nest.Territory.IsNestRoomCell(plant.Position);
                if (mana.StoredPct >= (indoor ? indoorBestPct : outdoorBestPct)) { continue; }
                if (indoorOnly && !nest.Territory.IsRoomCell(plant.Position)) { continue; }
                if (!indoor && (indoorBest != null || availableFertilizer - JobDriver_ViviNestFertilizePlant.FertilizerCountToFill(mana) < reserve)) { continue; }
                if (plant.IsForbidden(pawn) || plant.IsBurning() || !pawn.CanReserveAndReach(plant, PathEndMode.Touch, Danger.Some)) { continue; }

                if (indoor)
                {
                    indoorBest = plant;
                    indoorBestPct = mana.StoredPct;
                }
                else
                {
                    outdoorBest = plant;
                    outdoorBestPct = mana.StoredPct;
                }
            }
            return indoorBest ?? outdoorBest;
        }
    }
}
