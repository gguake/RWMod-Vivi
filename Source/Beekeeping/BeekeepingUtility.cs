using RimWorld;
using RimWorld.Planet;
using System;
using System.Collections.Generic;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace VVRace
{
    public static class BeekeepingUtility
    {
        public const int MaxPlayerViviCount = 4;

        public static bool IsWanderingViviFaction(this Faction faction) => faction != null && faction.def == VVFactionDefOf.VV_WanderingVivi;

        public static Faction CreateWanderingFaction()
        {
            var def = VVFactionDefOf.VV_WanderingVivi;
            var relations = new List<FactionRelation>();
            foreach (var other in Find.FactionManager.AllFactionsListForReading)
            {
                if (!other.def.PermanentlyHostileTo(def))
                {
                    relations.Add(new FactionRelation(other, FactionRelationKind.Neutral));
                }
            }

            var faction = FactionGenerator.NewGeneratedFactionWithRelations(def, relations, hidden: true);
            faction.temporary = true;
            Find.FactionManager.Add(faction);
            return faction;
        }

        public static bool IsFactionReservedByBeekeeping(Faction faction)
        {
            if (!faction.IsWanderingViviFaction()) { return false; }

            var maps = Find.Maps;
            for (int i = 0; i < maps.Count; ++i)
            {
                if (FindReservingLure(maps[i], faction) != null)
                {
                    return true;
                }
            }

            return false;
        }

        public static Building_ViviLure FindReservingLure(Map map, Faction faction)
        {
            var lures = map.listerBuildings.AllBuildingsColonistOfDef(VVThingDefOf.VV_ViviLure);
            for (int i = 0; i < lures.Count; ++i)
            {
                if (lures[i] is Building_ViviLure lure && lure.Spawned && lure.ReservedFaction == faction)
                {
                    return lure;
                }
            }

            return null;
        }

        public static void ReleaseWanderingFaction(Faction faction)
        {
            if (faction == null || !faction.temporary) { return; }

            Find.FactionManager.Notify_PawnLeftFaction(faction);
        }

        public static bool AnyNestOrAttraction(Map map, Building_ViviLure except = null)
        {
            var lures = map.listerBuildings.AllBuildingsColonistOfDef(VVThingDefOf.VV_ViviLure);
            for (int i = 0; i < lures.Count; ++i)
            {
                if (lures[i] is Building_ViviLure lure && lure != except && lure.InProgress)
                {
                    return true;
                }
            }

            return GetNestLord(map) != null;
        }

        public static void Notify_NestBabyHatched(Pawn baby, Thing hatchery)
        {
            var nest = GetNestLordJob(hatchery.Map, baby.Faction);
            if (nest == null || nest.lord.CurLordToil is LordToil_ViviNestExit) { return; }

            RemoveChemicalDependencies(baby);
            nest.lord.AddPawn(baby);
        }

        public static LordJob_ViviNest GetNestLordJob(Map map, Faction faction)
        {
            var nest = GetNestLord(map)?.LordJob as LordJob_ViviNest;
            return nest != null && nest.Faction == faction ? nest : null;
        }

        public static Lord GetNestLord(Map map)
        {
            var lords = map.lordManager.lords;
            for (int i = 0; i < lords.Count; ++i)
            {
                if (lords[i].LordJob is LordJob_ViviNest)
                {
                    return lords[i];
                }
            }

            return null;
        }

        public static void RemoveChemicalDependencies(Pawn pawn)
        {
            var genes = pawn?.genes;
            if (genes == null) { return; }

            var list = genes.GenesListForReading;
            for (int i = list.Count - 1; i >= 0; --i)
            {
                var def = list[i].def;
                if (def?.geneClass != null && typeof(Gene_ChemicalDependency).IsAssignableFrom(def.geneClass)) { genes.RemoveGene(list[i]); }
            }
        }

        public static void RemoveRandomGenes(Pawn pawn)
        {
            var genes = pawn?.genes;
            if (genes == null) { return; }

            var xenogenes = genes.Xenogenes;
            for (int i = xenogenes.Count - 1; i >= 0; --i)
            {
                if (ViviUtility.IsRandomGeneCandidate(xenogenes[i].def)) { genes.RemoveGene(xenogenes[i]); }
            }
        }

        public static bool IsBeekeepingMap(Map map)
        {
            if (map == null || !map.IsPlayerHome || map.IsPocketMap || map.IsTempIncidentMap) { return false; }
            if (map.Biome.inVacuum || map.Tile.LayerDef == PlanetLayerDefOf.Orbit) { return false; }

            return true;
        }

        public static int CountPlayerVivis(Map map)
        {
            var count = 0;
            var pawns = map.mapPawns.SpawnedPawnsInFaction(Faction.OfPlayer);
            for (int i = 0; i < pawns.Count; ++i)
            {
                var pawn = pawns[i];
                if (!pawn.Dead && pawn.RaceProps.Humanlike && pawn.IsVivi())
                {
                    count++;
                }
            }

            return count;
        }

        public static bool AnyRoyalVivi(Map map)
        {
            var pawns = map.mapPawns.AllHumanlikeSpawned;
            for (int i = 0; i < pawns.Count; ++i)
            {
                if (!pawns[i].Dead && pawns[i].IsRoyalVivi())
                {
                    return true;
                }
            }

            return false;
        }

        private const float MaxPlantGrowthRate = 4f;

        public static int GrowthReadyTick(Plant plant, float threshold, int within)
        {
            var now = Find.TickManager.TicksGame;
            var remaining = threshold - plant.Growth;
            if (remaining <= 0f) { return now + 1; }

            var growDays = plant.def.plant?.growDays ?? 0f;
            if (growDays <= 0f || remaining * GenDate.TicksPerDay * growDays / MaxPlantGrowthRate > within) { return -1; }

            var rate = plant.GrowthRate;
            if (rate <= 0f) { return -1; }

            var ticks = remaining * GenDate.TicksPerDay * growDays / rate;
            return ticks <= within ? now + UnityEngine.Mathf.Max(1, UnityEngine.Mathf.CeilToInt(ticks)) : -1;
        }

        public static int HarvestReadyTick(Plant plant, int within)
        {
            return plant.def.plant.Harvestable ? GrowthReadyTick(plant, plant.def.plant.harvestMinGrowth, within) : -1;
        }

        public static int EarlierTick(int tick, int candidate)
        {
            return candidate >= 0 && (tick < 0 || candidate < tick) ? candidate : tick;
        }

        public static bool OutdoorDangerous(Map map)
        {
            var conditions = map.GameConditionManager.ActiveConditions;
            for (int i = 0; i < conditions.Count; ++i)
            {
                var def = conditions[i].def;
                if (def == GameConditionDefOf.ToxicFallout || def.preventNeutralVisitors)
                {
                    return true;
                }

                if (ModsConfig.BiotechActive && def == GameConditionDefOf.NoxiousHaze)
                {
                    return true;
                }
            }

            return false;
        }

        public static bool OutdoorWorkPaused(Pawn pawn) => JobGiver_ViviNestRest.IsNight(pawn) || OutdoorDangerous(pawn.Map);

        private static List<ThingStuffPair> _basicApparel;
        public static FloatRange WanderingViviSafeTemperature
        {
            get
            {
                if (_basicApparel == null)
                {
                    _basicApparel = new List<ThingStuffPair> { new ThingStuffPair(VVThingDefOf.VV_BasicWorkerCostume, ThingDefOf.Cloth) };
                }

                return GenTemperature.SafeTemperatureRange(VVThingDefOf.VV_Vivi, _basicApparel);
            }
        }

        public static bool TemperatureSuitable(Map map)
        {
            var safe = WanderingViviSafeTemperature;
            return safe.Includes(map.mapTemperature.OutdoorTemp) && safe.Includes(map.mapTemperature.SeasonalTemp);
        }

        public static bool FlowerSeasonNow(Map map) => PlantUtility.GrowthSeasonNow(map, ThingDefOf.Plant_Dandelion);

        public static bool AnyHostileThreat(Map map) => GenHostility.AnyHostileActiveThreatTo(map, Faction.OfPlayer);

        public static readonly (Func<Map, bool> met, string reasonKey, Func<string> rowLabel, string tipKey)[] ArrivalConditions =
        {
            (map => IsBeekeepingMap(map), LocalizeString_Inspector.VV_Inspector_LureConditionMap,
                () => LocalizeString_ITab.VV_ITab_ViviLure_Map.Translate(), null),
            (map => !AnyRoyalVivi(map), LocalizeString_Inspector.VV_Inspector_LureConditionRoyal,
                () => LocalizeString_ITab.VV_ITab_ViviLure_NoRoyal.Translate(), null),
            (map => CountPlayerVivis(map) <= MaxPlayerViviCount, LocalizeString_Inspector.VV_Inspector_LureConditionViviCount,
                () => LocalizeString_ITab.VV_ITab_ViviLure_ViviCount.Translate(MaxPlayerViviCount), null),
            (map => !AnyHostileThreat(map), LocalizeString_Inspector.VV_Inspector_LureConditionHostile,
                () => LocalizeString_ITab.VV_ITab_ViviLure_NoHostile.Translate(), null),
            (map => TemperatureSuitable(map), LocalizeString_Inspector.VV_Inspector_LureConditionTemperature,
                () => LocalizeString_ITab.VV_ITab_ViviLure_Temperature.Translate(), LocalizeString_ITab.VV_ITab_ViviLure_TemperatureTip),
            (map => FlowerSeasonNow(map), LocalizeString_Inspector.VV_Inspector_LureConditionSeason,
                () => LocalizeString_ITab.VV_ITab_ViviLure_Season.Translate(), null),
            (map => !OutdoorDangerous(map), LocalizeString_Inspector.VV_Inspector_LureConditionWeather,
                () => LocalizeString_ITab.VV_ITab_ViviLure_Weather.Translate(), LocalizeString_ITab.VV_ITab_ViviLure_WeatherTip),
        };

        public static bool CheckArrivalConditions(Map map, out string reason)
        {
            var conditions = ArrivalConditions;
            for (int i = 0; i < conditions.Length; ++i)
            {
                if (!conditions[i].met(map))
                {
                    reason = conditions[i].reasonKey.Translate();
                    return false;
                }
            }

            reason = null;
            return true;
        }

        public static LordJob_ViviNest GetNestLordJob(this Pawn pawn) => pawn.GetLord()?.LordJob as LordJob_ViviNest;

        public static LordJob_ViviNest SettledNestOf(Pawn pawn)
        {
            var nest = pawn.GetNestLordJob();
            if (nest == null || !nest.Settled || pawn.DevelopmentalStage.Baby()) { return null; }
            return nest;
        }

        public static LordJob_ViviNest OutdoorWorkingNest(Pawn pawn, WorkTypeDef workType = null)
        {
            var nest = SettledNestOf(pawn);
            if (nest == null || (workType != null && pawn.WorkTypeIsDisabled(workType))) { return null; }
            return OutdoorWorkPaused(pawn) ? null : nest;
        }

        public static bool NeedsNestFeeding(Pawn pawn)
        {
            return pawn.Downed && pawn.needs?.food != null && !pawn.DevelopmentalStage.Baby() && pawn.Faction.IsWanderingViviFaction() && pawn.GetNestLordJob() != null;
        }

        public static void AddSourcesDistinct(Transition transition, IEnumerable<LordToil> toils)
        {
            foreach (var toil in toils)
            {
                if (!transition.sources.Contains(toil))
                {
                    transition.AddSource(toil);
                }
            }
        }

        public static bool TryFindEntryCellReaching(Map map, IntVec3 destination, out IntVec3 entryCell)
        {
            return RCellFinder.TryFindRandomPawnEntryCell(
                out entryCell,
                map,
                CellFinder.EdgeRoadChance_Neutral,
                allowFogged: false,
                extraValidator: c => map.reachability.CanReach(c, destination, PathEndMode.Touch, TraverseParms.For(TraverseMode.PassDoors)));
        }

        public static bool IsStructure(Thing thing) => thing is Building || thing.def.IsBlueprint || thing.def.IsFrame;

        public static bool TouchesWall(IntVec3 cell, Map map)
        {
            foreach (var dir in GenAdj.CardinalDirections)
            {
                var adj = cell + dir;
                var edifice = adj.InBounds(map) ? adj.GetEdifice(map) : null;
                if (edifice != null && edifice.def.passability == Traversability.Impassable) { return true; }
            }
            return false;
        }

        public static void MakeStacks(ThingDef def, int count, List<Thing> into)
        {
            if (def == null) { return; }

            while (count > 0)
            {
                var thing = ThingMaker.MakeThing(def);
                thing.stackCount = UnityEngine.Mathf.Min(count, def.stackLimit);
                count -= thing.stackCount;
                into.Add(thing);
            }
        }

        public static void PlaceProduct(Pawn pawn, Thing thing, IntVec3 cell)
        {
            var nest = pawn.GetNestLordJob();
            var placed = nest != null ? nest.TryPlaceOwned(thing, cell) : GenPlace.TryPlaceThing(thing, cell, pawn.Map, ThingPlaceMode.Near);
            if (!placed && !thing.Destroyed)
            {
                thing.Destroy();
            }
        }
    }
}
