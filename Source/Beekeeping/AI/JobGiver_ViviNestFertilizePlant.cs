using RimWorld;
using Verse;
using Verse.AI;

namespace VVRace
{
    // 저장 마나가 낮은 둥지 마법식물(화분의 잔불꽃 등)에 둥지 몫 마나 비료를 가져가 저장 마나를 채운다.
    // 잔불꽃은 마나가 있을 때만 열을 내고 자기 칸의 환경 마나만 흡수해서, 방출기와 떨어진 둥지는 겨울 동안 저장이 700→50으로 줄었다(장기 계측).
    // 마법식물에는 연료 칸(CompRefuelable)이 없어 바닐라 연료 보충을 쓸 수 없으므로, 연료 보충과 같은 순서의 둥지 잡으로 준다.
    // 비료를 주는 일은 새싹 영양·방출기 연료처럼 바닐라 연료 보충과 같은 운반 작업으로 친다.
    public class JobGiver_ViviNestFertilizePlant : ThinkNode_JobGiver
    {
        // 저장 마나가 이 비율 아래로 내려가면 가득 채운다. 생존용 잔불꽃(저장 700, 마나를 못 받으면 하루 200 소모)은 절반이면 1.75일을 버티므로
        // 밤잠(8시간)이나 나쁜 날씨로 미뤄져도 마르기 전에 채울 수 있고, 조금 빈 식물을 채우러 자주 오가지 않는다.
        public const float LowManaPct = 0.5f;

        protected override Job TryGiveJob(Pawn pawn)
        {
            var nest = pawn.GetNestLordJob();
            if (nest == null || !nest.Settled || pawn.DevelopmentalStage.Baby() || pawn.WorkTypeIsDisabled(WorkTypeDefOf.Hauling)) { return null; }
            if (nest.Stock.CountOf(VVThingDefOf.VV_Fertilizer) <= 0) { return null; }

            // 밤이거나 바깥이 위험하면 바깥 일은 하지 않는다: 방 안 식물에 방 안 비료만 준다.
            var indoorOnly = IndoorOnly(pawn);
            var plant = FindPlant(pawn, nest, indoorOnly);
            if (plant == null) { return null; }

            var fertilizer = GenClosest.ClosestThingReachable(
                pawn.Position,
                pawn.Map,
                ThingRequest.ForDef(VVThingDefOf.VV_Fertilizer),
                PathEndMode.ClosestTouch,
                TraverseParms.For(pawn, Danger.Some),
                validator: t => IsUsableFertilizer(t, pawn, nest, indoorOnly) && pawn.CanReserve(t));

            return fertilizer != null ? JobMaker.MakeJob(VVJobDefOf.VV_ViviNestFertilizePlant, plant, fertilizer) : null;
        }

        public static bool IndoorOnly(Pawn pawn) => JobGiver_ViviNestRest.IsNight(pawn) || BeekeepingUtility.OutdoorDangerous(pawn.Map);

        public static bool IsUsableFertilizer(Thing thing, Pawn pawn, LordJob_ViviNest nest, bool indoorOnly)
        {
            return nest.Stock.IsNestOwned(thing) && (!indoorOnly || nest.Territory.IsRoomCell(thing.Position));
        }

        // 둥지 세력의 마법식물 중 저장 마나 비율이 가장 낮은 것(플레이어 식물은 세력이 달라 보지 않는다)
        private static ArcanePlant FindPlant(Pawn pawn, LordJob_ViviNest nest, bool indoorOnly)
        {
            ArcanePlant best = null;
            var bestPct = LowManaPct;
            var buildings = pawn.Map.listerBuildings.allBuildingsNonColonist;
            for (int i = 0; i < buildings.Count; ++i)
            {
                if (!(buildings[i] is ArcanePlant plant) || plant.Faction != nest.Faction) { continue; }

                // 새싹(저장 칸 없음, 영양은 연료 보충이 채운다)·영원꽃처럼 마나를 저장하지 않는 것과 무한 마나는 뺀다.
                var mana = plant.ManaComp;
                if (mana == null || mana.Props.manaCapacity <= 0f || mana.IsUnlimited || mana.StoredPct >= bestPct) { continue; }
                if (indoorOnly && !nest.Territory.IsRoomCell(plant.Position)) { continue; }
                if (plant.IsForbidden(pawn) || plant.IsBurning() || !pawn.CanReserveAndReach(plant, PathEndMode.Touch, Danger.Some)) { continue; }

                best = plant;
                bestPct = mana.StoredPct;
            }
            return best;
        }
    }
}
