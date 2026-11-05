using RimWorld;
using System.Collections.Generic;
using Verse;
using Verse.AI;

namespace VVRace
{
    // 둥지 몫 마법식물 씨앗을 기존 씨앗 심기 잡으로 심는다.
    // 생존용 꽃(잔불꽃)은 방 안 둥지 화분에, 나머지(풍년화·미지의 씨앗)는 둥지 가까이 마나·비옥도가 좋은 바깥 땅에 심는다.
    // 서리꽃은 방을 얼리므로 화분에 심지 않는다(바깥에 심는다).
    public class JobGiver_ViviNestPlantSeed : ThinkNode_JobGiver
    {
        private const float OutdoorSearchRadius = 12f;
        // 바깥 자리 점수 = 환경 마나 + 비옥도 × 이 값. 풍년화는 비옥도 1.2 이상이면 스스로 마나를 남기고(1.0이면 하루 150 모자람),
        // 마나 방출기 곁은 환경 마나로 채운다. 실험에서 방출기 칸 마나는 26 남짓이라 '마나 30 이상'만 찾으면 아무 데도 심지 못했다.
        private const float FertilityWeight = 60f;
        private const float MinFertility = 0.7f;

        private static List<ThingDef> _seedDefs;

        private static List<ThingDef> SeedDefs
        {
            get
            {
                if (_seedDefs == null)
                {
                    _seedDefs = new List<ThingDef>();
                    foreach (var def in DefDatabase<ThingDef>.AllDefsListForReading)
                    {
                        if (def.HasComp(typeof(CompArcaneSeed))) { _seedDefs.Add(def); }
                    }
                }
                return _seedDefs;
            }
        }

        public static bool IsPotPlant(ThingDef plantDef)
        {
            return plantDef != null && plantDef == VVThingDefOf.VV_EmberBloom;
        }

        protected override Job TryGiveJob(Pawn pawn)
        {
            var nest = pawn.GetNestLordJob();
            if (nest == null || !nest.Settled || pawn.DevelopmentalStage.Baby()) { return null; }
            if (JobGiver_ViviNestRest.IsNight(pawn)) { return null; }

            var map = pawn.Map;
            foreach (var seedDef in SeedDefs)
            {
                var seeds = map.listerThings.ThingsOfDef(seedDef);
                for (int i = 0; i < seeds.Count; ++i)
                {
                    var seed = seeds[i];
                    var comp = seed.TryGetComp<CompArcaneSeed>();
                    if (comp == null || !nest.Stock.IsNestOwned(seed)) { continue; }
                    if (!pawn.CanReserveAndReach(seed, PathEndMode.Touch, Danger.Some)) { continue; }

                    var potPlant = IsPotPlant(comp.Props.targetPlantDef);
                    if (!potPlant && BeekeepingUtility.OutdoorDangerous(map)) { continue; }

                    var cell = potPlant ? FindPotCell(nest, comp, pawn) : FindOutdoorCell(nest, comp, pawn);
                    if (!cell.IsValid) { continue; }

                    // 바닐라 재배처럼 자리를 막는 풀은 먼저 벤다(마법식물은 건물이라 여기 잡히지 않는다).
                    var plant = cell.GetPlant(map);
                    if (plant != null)
                    {
                        if (!pawn.CanReserve(plant) || !PlantUtility.PawnWillingToCutPlant_Job(plant, pawn)) { continue; }
                        return JobMaker.MakeJob(JobDefOf.CutPlant, plant);
                    }

                    // 씨앗 한 알만 들고 가 심는다(심기 잡이 이 자리 목록을 들고 간 씨앗으로 옮긴다).
                    comp.SeedlingCells.Clear();
                    comp.SeedlingCells.Add(cell);
                    var job = JobMaker.MakeJob(VVJobDefOf.VV_PlantArcaneSeed, seed, cell);
                    job.count = 1;
                    return job;
                }
            }

            return null;
        }

        private static IntVec3 FindPotCell(LordJob_ViviNest nest, CompArcaneSeed comp, Pawn pawn)
        {
            var map = pawn.Map;
            foreach (var building in map.listerBuildings.allBuildingsNonColonist)
            {
                if (!(building is ArcanePlantPot pot) || pot.Faction != nest.Faction || !nest.Territory.IsRoomCell(pot.Position)) { continue; }

                var cell = pot.Position;
                if (cell.GetFirstThing<ArcanePlant>(map) != null || cell.GetFirstThing<ArcanePlant_Seedling>(map) != null) { continue; }
                if (!comp.CanSowAt(cell, map).Accepted || !pawn.CanReserveAndReach(cell, PathEndMode.Touch, Danger.Some)) { continue; }

                return cell;
            }
            return IntVec3.Invalid;
        }

        private static IntVec3 FindOutdoorCell(LordJob_ViviNest nest, CompArcaneSeed comp, Pawn pawn)
        {
            var map = pawn.Map;
            var mana = map.GetManaComponent();
            if (mana == null) { return IntVec3.Invalid; }

            var best = IntVec3.Invalid;
            var bestScore = 0f;
            var cellCount = GenRadial.NumCellsInRadius(OutdoorSearchRadius);
            for (int i = 0; i < cellCount; ++i)
            {
                var cell = nest.NestCell + GenRadial.RadialPattern[i];
                if (!cell.InBounds(map)) { continue; }

                var fertility = map.fertilityGrid.FertilityAt(cell);
                var score = mana[cell] + fertility * FertilityWeight;
                if (fertility < MinFertility || score <= bestScore) { continue; }
                if (!nest.Planner.IsFreeOutdoorCell(cell, allowPlants: true)) { continue; }
                if (!comp.CanSowAt(cell, map).Accepted || !pawn.CanReach(cell, PathEndMode.Touch, Danger.Some)) { continue; }

                best = cell;
                bestScore = score;
            }
            return best;
        }
    }
}
