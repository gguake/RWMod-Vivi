using RimWorld;
using RimWorld.Planet;
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

        // 바닐라 거지 퀘스트처럼 영구 적대가 아닌 모든 세력과 중립으로 시작하는 숨김 임시 세력을 만든다.
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

        // 임시 세력은 소속 폰이 모두 떠나면 바닐라가 지우므로, 유인통·예약된 정착대가 들고 있는 동안은 붙잡아 둔다.
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

            // 발동 시각이 된 예약은 지금 처리 중이므로(불발이면 세력을 놓아 준다) 붙잡지 않는다.
            var ticks = Find.TickManager.TicksGame;
            foreach (QueuedIncident queued in Find.Storyteller.incidentQueue)
            {
                if (IsQueuedSettlers(queued, faction) && queued.FireTick > ticks)
                {
                    return true;
                }
            }

            return false;
        }

        // 그 세력을 붙잡고 있는(정찰 중·정착대 대기) 유인통
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

        // 그 세력의 정착대 예약 인시던트인지
        public static bool IsQueuedSettlers(QueuedIncident queued, Faction faction)
        {
            var firing = queued.FiringIncident;
            return firing?.def == VVIncidentDefOf.VV_ViviNestSettlers && firing.parms?.faction == faction;
        }

        // 붙잡기를 푼 세력은 다음 퇴장 이벤트가 없으면 남아 있으므로 바닐라 삭제 검사를 한 번 돌려 준다.
        public static void ReleaseWanderingFaction(Faction faction)
        {
            if (faction == null || !faction.temporary) { return; }

            Find.FactionManager.Notify_PawnLeftFaction(faction);
        }

        // 맵마다 유인(방문·정착대 대기)이나 둥지는 하나만 둔다. except는 묻는 유인통 자신(유인 조건 탭)이라 세지 않는다.
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

        // 둥지 부화기에서 깬 아기는 둥지 무리에 든다. 둥지 안 일이라 플레이어에게 알리지 않는다.
        public static void Notify_NestBabyHatched(Pawn baby, Thing hatchery)
        {
            var lord = GetNestLord(hatchery.Map);
            if (lord == null || ((LordJob_ViviNest)lord.LordJob).Faction != baby.Faction) { return; }

            RemoveChemicalDependencies(baby);
            lord.AddPawn(baby);
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

        // 둥지에는 약이 없으므로 화학 의존 유전자(비비 무작위 유전자로 붙는다)를 지니면 금단으로 쓰러져 굶어 죽는다(장기 계측). 둥지 비비에게서는 뺀다.
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

        // 처음 합류하는 정착대는 비비 무작위 유전자 없이 온다(사용자 요구). 무작위 유전자는 생성 때 이종 유전자가 하나도 없는 NPC 비비에게
        // 무작위 후보(ViviUtility.IsRandomGeneCandidate)에서 골라 이종 유전자로 붙고(ViviRacePatch.PawnGenerator_GenerateGenes_Postfix), 생성 요청에는 이를 끌 옵션이 없다.
        // 그래서 생성한 뒤 무작위 후보인 이종 유전자만 지운다. 비비 제노타입 유전자는 생식 유전자(내생)이고 무작위 후보에서도 빠지므로 남는다.
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

        // 식물 성장 속도 상한(비옥한 땅의 비옥도 배율 등): 이 속도로도 기한 안에 자라지 못하는 식물은 성장 속도를 재지 않는다.
        private const float MaxPlantGrowthRate = 4f;

        // 식물의 성장도가 threshold에 닿을 틱(지금 성장 속도로 셈, 밤에 쉬는 시간을 빼지 않아 실제보다 이르다). within틱 안에 닿지 않거나 자라지 않으면 -1.
        // 둥지의 "지금 할 수 없음" 기억이 다음에 볼 틱을 정할 때 쓴다(ViviNestProduction).
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

        // 거둘 수 있게 자랄 틱(GrowthReadyTick, 바닐라 수확 하한 harvestMinGrowth). 거두는 식물이 아니면 -1.
        public static int HarvestReadyTick(Plant plant, int within)
        {
            return plant.def.plant.Harvestable ? GrowthReadyTick(plant, plant.def.plant.harvestMinGrowth, within) : -1;
        }

        // 두 준비 틱(모르면 -1) 중 이른 것
        public static int EarlierTick(int tick, int candidate)
        {
            return candidate >= 0 && (tick < 0 || candidate < tick) ? candidate : tick;
        }

        // 바깥 활동이 위험한 날씨·상태(독성 낙진, 유독 연무, 혈우 등)인지
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

        // 밤이거나 바깥이 위험하면 바깥 일(채집·나무 베기·약뿌리 거두기·땅 비료·정원)을 쉰다. 비료 주기는 이때 방 안 식물에 방 안 비료만 준다.
        public static bool OutdoorWorkPaused(Pawn pawn) => JobGiver_ViviNestRest.IsNight(pawn) || OutdoorDangerous(pawn.Map);

        // 떠돌이 비비는 기본옷만 입으므로 그 옷을 입은 비비의 안전 기온으로 본다.
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

        // 아래 조건 하나하나는 판정(CheckArrivalConditions)과 유인 조건 탭(Building_ViviLure.ConditionRows)이 함께 쓴다.
        public static bool TemperatureSuitable(Map map)
        {
            var safe = WanderingViviSafeTemperature;
            return safe.Includes(map.mapTemperature.OutdoorTemp) && safe.Includes(map.mapTemperature.SeasonalTemp);
        }

        // 꿀을 딸 꽃이 자라는 철이어야 한다.
        public static bool FlowerSeasonNow(Map map) => PlantUtility.GrowthSeasonNow(map, ThingDefOf.Plant_Dandelion);

        public static bool AnyHostileThreat(Map map) => GenHostility.AnyHostileActiveThreatTo(map, Faction.OfPlayer);

        // 방문·정착대 도착 공통 조건. 유인통 자체(미끼·진행 상태)는 호출하는 쪽이 따로 본다.
        public static bool CheckArrivalConditions(Map map, out string reason)
        {
            if (!IsBeekeepingMap(map))
            {
                reason = LocalizeString_Inspector.VV_Inspector_LureConditionMap.Translate();
                return false;
            }

            if (AnyRoyalVivi(map))
            {
                reason = LocalizeString_Inspector.VV_Inspector_LureConditionRoyal.Translate();
                return false;
            }

            if (CountPlayerVivis(map) > MaxPlayerViviCount)
            {
                reason = LocalizeString_Inspector.VV_Inspector_LureConditionViviCount.Translate();
                return false;
            }

            if (AnyHostileThreat(map))
            {
                reason = LocalizeString_Inspector.VV_Inspector_LureConditionHostile.Translate();
                return false;
            }

            if (!TemperatureSuitable(map))
            {
                reason = LocalizeString_Inspector.VV_Inspector_LureConditionTemperature.Translate();
                return false;
            }

            if (!FlowerSeasonNow(map))
            {
                reason = LocalizeString_Inspector.VV_Inspector_LureConditionSeason.Translate();
                return false;
            }

            if (OutdoorDangerous(map))
            {
                reason = LocalizeString_Inspector.VV_Inspector_LureConditionWeather.Translate();
                return false;
            }

            reason = null;
            return true;
        }

        public static LordJob_ViviNest GetNestLordJob(this Pawn pawn) => pawn.GetLord()?.LordJob as LordJob_ViviNest;

        // 누군가 먹여 줘야 하는 둥지 구성원: 쓰러진 둥지 비비(아기 제외, 아기는 육아의 젖병 먹이기가 맡는다).
        // 둥지 먹이기 잡 기버가 대상을 고르고, 바닐라 환자 먹이기 잡은 이 판정이 참인 동안 이어 간다(BeekeepingPatch).
        public static bool NeedsNestFeeding(Pawn pawn)
        {
            return pawn.Downed && pawn.needs?.food != null && !pawn.DevelopmentalStage.Baby() && pawn.Faction.IsWanderingViviFaction() && pawn.GetNestLordJob() != null;
        }

        // 하위 그래프 토일을 출발점에 더할 때 이미 넣은 토일이 겹치면 바닐라가 오류를 남긴다.
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

        // 건물·청사진·골조(둥지 칸 고르기가 피하는 것)
        public static bool IsStructure(Thing thing) => thing is Building || thing.def.IsBlueprint || thing.def.IsFrame;

        // 네 방향 이웃에 지나갈 수 없는 구조물(벽 등)이 있는 칸
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

        // def를 count개 만들어 더미 한도씩 나눠 into에 더한다.
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

        // 구성원이 만든 산출물(채취물·땅 비료)을 cell 둘레에 둥지 몫으로 놓는다(둥지를 떠났으면 그냥 놓는다). 놓지 못하면 없앤다.
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
