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
                var lures = maps[i].listerBuildings.AllBuildingsColonistOfDef(VVThingDefOf.VV_ViviLure);
                foreach (var lure in lures)
                {
                    if (lure is Building_ViviLure viviLure && viviLure.ReservedFaction == faction)
                    {
                        return true;
                    }
                }
            }

            // 발동 시각이 된 예약은 지금 처리 중이므로(불발이면 세력을 놓아 준다) 붙잡지 않는다.
            var ticks = Find.TickManager.TicksGame;
            foreach (QueuedIncident queued in Find.Storyteller.incidentQueue)
            {
                var firing = queued.FiringIncident;
                if (firing?.def == VVIncidentDefOf.VV_ViviNestSettlers && firing.parms?.faction == faction && queued.FireTick > ticks)
                {
                    return true;
                }
            }

            return false;
        }

        // 붙잡기를 푼 세력은 다음 퇴장 이벤트가 없으면 남아 있으므로 바닐라 삭제 검사를 한 번 돌려 준다.
        public static void ReleaseWanderingFaction(Faction faction)
        {
            if (faction == null || !faction.temporary) { return; }

            Find.FactionManager.Notify_PawnLeftFaction(faction);
        }

        // 맵마다 유인(방문·정착대 대기)이나 둥지는 하나만 둔다.
        public static bool AnyNestOrAttraction(Map map)
        {
            var lures = map.listerBuildings.AllBuildingsColonistOfDef(VVThingDefOf.VV_ViviLure);
            for (int i = 0; i < lures.Count; ++i)
            {
                if (lures[i] is Building_ViviLure lure && lure.InProgress)
                {
                    return true;
                }
            }

            return GetNestLord(map) != null;
        }

        // 둥지 부화기에서 깬 아기는 둥지 무리에 들고, 지켜보는 플레이어에게 알린다.
        public static void Notify_NestBabyHatched(Pawn baby, Thing hatchery)
        {
            var lord = GetNestLord(hatchery.Map);
            if (lord == null || ((LordJob_ViviNest)lord.LordJob).Faction != baby.Faction) { return; }

            RemoveChemicalDependencies(baby);
            lord.AddPawn(baby);
            Messages.Message(LocalizeString_Message.VV_Message_ViviNestBabyHatched.Translate(baby.Faction.Name), baby, MessageTypeDefOf.PositiveEvent);
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
        public static bool IsChemicalDependency(GeneDef def)
        {
            return def?.geneClass != null && typeof(Gene_ChemicalDependency).IsAssignableFrom(def.geneClass);
        }

        public static void RemoveChemicalDependencies(Pawn pawn)
        {
            var genes = pawn?.genes;
            if (genes == null) { return; }

            var list = genes.GenesListForReading;
            for (int i = list.Count - 1; i >= 0; --i)
            {
                if (IsChemicalDependency(list[i].def)) { genes.RemoveGene(list[i]); }
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

        public static bool ClimateSuitable(Map map, out string reason)
        {
            var safe = WanderingViviSafeTemperature;
            if (!safe.Includes(map.mapTemperature.OutdoorTemp) || !safe.Includes(map.mapTemperature.SeasonalTemp))
            {
                reason = LocalizeString_Inspector.VV_Inspector_LureConditionTemperature.Translate();
                return false;
            }

            // 꿀을 딸 꽃이 자라는 철이어야 한다.
            if (!PlantUtility.GrowthSeasonNow(map, ThingDefOf.Plant_Dandelion))
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

            var viviCount = CountPlayerVivis(map);
            if (viviCount > MaxPlayerViviCount)
            {
                reason = LocalizeString_Inspector.VV_Inspector_LureConditionViviCount.Translate(viviCount, MaxPlayerViviCount);
                return false;
            }

            if (GenHostility.AnyHostileActiveThreatTo(map, Faction.OfPlayer))
            {
                reason = LocalizeString_Inspector.VV_Inspector_LureConditionHostile.Translate();
                return false;
            }

            return ClimateSuitable(map, out reason);
        }

        public static LordJob_ViviNest GetNestLordJob(this Pawn pawn) => pawn.GetLord()?.LordJob as LordJob_ViviNest;

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
    }
}
