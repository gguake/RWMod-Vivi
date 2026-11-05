using RimWorld;
using System.Collections.Generic;
using Verse;
using Verse.AI;

namespace VVRace
{
    // 둥지 몫 마법식물 씨앗을 기존 씨앗 심기 잡으로 심는다.
    // 생존용 꽃(잔불꽃)은 둥지 방 화분에(잔불꽃이 없는 방부터, 그다음 방마다 상한까지 추운 방부터, ViviNestPlanner.FindEmberPotCell), 나머지(미지의 씨앗 등)는 둥지 가까이 새싹이 자랄 만큼 마나가 있는 바깥 땅에 심는다.
    // 화분은 잔불꽃만 쓴다(미지의 씨앗은 전투·장식 식물로도 자라므로 방 화분을 차지하지 않게 바깥에 심는다).
    // 서리꽃은 방을 얼리므로 화분에 심지 않는다(바깥에 심는다). 정착대가 가져오는 잔불꽃·풍년화는 씨앗이 아니라 포장된 식물이라 설계자가 설치한다(ViviNestPlanner).
    // 상한이 남은 방에 빈 화분이 없으면 설계자가 화분을 지으므로 씨앗은 그때까지 둥지에 남는다.
    public class JobGiver_ViviNestPlantSeed : ThinkNode_JobGiver
    {
        // 포장 식물을 바깥에 설치할 자리도 같은 반경·비옥도 하한을 쓴다(ViviNestPlanner.FindOutdoorInstallCell).
        public const float OutdoorSearchRadius = 12f;
        // 바깥 자리는 환경 마나가 이만큼 있는 칸만 쓴다. 새싹은 종류와 상관없이 제 칸 환경 마나를 하루 80 흡수해 그대로 쓰고(저장 0)
        // 모자라면 자라지 않는데, 비료는 마나와 상관없이 하루 10개씩 태운다. 새싹은 제 칸 마나를 3.5~3.8 끌어내린다.
        // 게임 안 계측(방출기 하나): 둘레 마나는 1칸 19·3칸 11·5칸 7·7칸 5·9칸 3이고, 7칸(4.8)에 둔 새싹은 늘 자랐지만 9칸(3.3)은 88%만 자랐다.
        // 5면 나중에 곁에 심긴 새싹 몫도 남는다. 예전 기준(마나 30 이상)은 방출기 칸(26 남짓)보다 높아 아무 데도 못 심었고, 그 뒤 쓴 점수(마나 + 비옥도×60)는
        // 비옥한 먼 칸을 골라 장기 계측(온대 60일)에서 새싹 14그루가 방출기에서 8~10칸 떨어진 마나 0 칸에서 자라지 못한 채 비료만 먹었다.
        private const float MinSeedlingMana = 5f;
        public const float MinFertility = 0.7f;

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
            return nest.Planner.FindEmberPotCell(cell => comp.CanSowAt(cell, map).Accepted && pawn.CanReserveAndReach(cell, PathEndMode.Touch, Danger.Some));
        }

        // 새싹이 자랄 만큼 환경 마나가 있는 칸 중 비옥도가 가장 높은 칸(같으면 마나가 많은 칸). 그런 칸이 없으면 씨앗은 둥지에 남겨 둔다.
        // 이미 심긴 새싹·식물이 마나를 흡수해 둘레 마나가 내려가므로 방출기 하나로 자랄 만큼만 심긴다.
        private static IntVec3 FindOutdoorCell(LordJob_ViviNest nest, CompArcaneSeed comp, Pawn pawn)
        {
            var map = pawn.Map;
            var mana = map.GetManaComponent();
            if (mana == null) { return IntVec3.Invalid; }

            var best = IntVec3.Invalid;
            var bestFertility = 0f;
            var bestMana = 0f;
            var cellCount = GenRadial.NumCellsInRadius(OutdoorSearchRadius);
            for (int i = 0; i < cellCount; ++i)
            {
                var cell = nest.NestCell + GenRadial.RadialPattern[i];
                if (!cell.InBounds(map)) { continue; }

                var cellMana = mana[cell];
                if (cellMana < MinSeedlingMana) { continue; }
                var fertility = map.fertilityGrid.FertilityAt(cell);
                if (fertility < MinFertility || fertility < bestFertility || (fertility == bestFertility && cellMana <= bestMana)) { continue; }
                if (!nest.Planner.IsFreeOutdoorCell(cell, allowPlants: true)) { continue; }
                if (!comp.CanSowAt(cell, map).Accepted || !pawn.CanReach(cell, PathEndMode.Touch, Danger.Some)) { continue; }

                best = cell;
                bestFertility = fertility;
                bestMana = cellMana;
            }
            return best;
        }
    }
}
