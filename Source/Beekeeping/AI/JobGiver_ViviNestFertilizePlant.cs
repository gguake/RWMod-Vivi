using RimWorld;
using Verse;
using Verse.AI;

namespace VVRace
{
    // 저장 마나가 낮은 둥지 마법식물(화분의 잔불꽃 등)에 둥지 몫 마나 비료를 가져가 저장 마나를 채운다.
    // 잔불꽃은 마나가 있을 때만 열을 내고 자기 칸의 환경 마나만 흡수해서, 방출기와 떨어진 둥지는 겨울 동안 저장이 700→50으로 줄었다(장기 계측).
    // 마법식물에는 연료 칸(CompRefuelable)이 없어 바닐라 연료 보충을 쓸 수 없으므로, 연료 보충과 같은 순서의 둥지 잡으로 준다.
    // 비료를 주는 일은 새싹 영양·방출기 연료처럼 바닐라 연료 보충과 같은 운반 작업으로 친다.
    // 방 안 식물을 먼저 채우고 바깥 식물은 예비분이 남을 때만 채운다. 장기 계측(툰드라 60일)에서 비료 주기 105번 대부분이 미지의 씨앗에서 자란
    // 바깥 전투·장식 식물에 쓰여 둥지 비료가 몇 번 0이 됐는데, 겨울 난방은 방 안 잔불꽃과 그 마나를 대는 방출기(비료 하루 8개)가 맡는다.
    public class JobGiver_ViviNestFertilizePlant : ThinkNode_JobGiver
    {
        // 저장 마나가 이 비율 아래로 내려가면 가득 채운다. 생존용 잔불꽃(저장 700, 마나를 못 받으면 하루 200 소모)은 절반이면 1.75일을 버티므로
        // 밤잠(8시간)이나 나쁜 날씨로 미뤄져도 마르기 전에 채울 수 있고, 조금 빈 식물을 채우러 자주 오가지 않는다.
        public const float LowManaPct = 0.5f;

        // 바깥 식물에는 주고 나서도 둥지 몫 비료가 이만큼 남을 때만 준다(방 안 식물은 제한 없이 준다). 비료 비축 목표의 절반으로,
        // 가장 작은 목표(120)에서도 마나 방출기를 두 번 채울 만큼이다(바닐라 자동 연료 보충은 연료가 30% 아래일 때 가득 채우므로 40개 중 28개 남짓).
        // 순서만 바꾸면 바깥 식물이 마지막 비료를 먹은 뒤 방출기·방 안 잔불꽃이 굶으므로, 예비분이 있어야 방 안 먼저가 시간이 지나도 지켜진다.
        public static int OutdoorReserve(LordJob_ViviNest nest) => nest.Production.FertilizerTarget / 2;

        protected override Job TryGiveJob(Pawn pawn)
        {
            var nest = pawn.GetNestLordJob();
            if (nest == null || !nest.Settled || pawn.DevelopmentalStage.Baby() || pawn.WorkTypeIsDisabled(WorkTypeDefOf.Hauling)) { return null; }
            if (nest.Stock.CountOf(VVThingDefOf.VV_Fertilizer) <= 0) { return null; }

            // 밤이거나 바깥이 위험하면 바깥 일은 하지 않는다: 방 안 식물에 방 안 비료만 준다.
            var indoorOnly = BeekeepingUtility.OutdoorWorkPaused(pawn);
            var plant = FindPlant(pawn, nest, indoorOnly, AvailableFertilizer(pawn, nest));
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

        public static bool IsUsableFertilizer(Thing thing, Pawn pawn, LordJob_ViviNest nest, bool indoorOnly)
        {
            return nest.Stock.IsNestOwned(thing) && (!indoorOnly || nest.Territory.IsRoomCell(thing.Position));
        }

        // 방 안 식물: 둥지 방 칸(벽이 뚫린 동안은 원래 방 칸)의 식물. 둥지 방을 데우는 화분의 잔불꽃이 여기 있고, 나머지 씨앗은 바깥 땅에 심는다.
        // 바닐라 방 판정(Room.UsesOutdoorTemperature)은 벽이 뚫린 동안 둥지 방을 바깥으로 쳐서 난방이 가장 급할 때 잔불꽃을 뒤로 미루므로 쓰지 않는다.
        private static bool IsIndoorPlant(LordJob_ViviNest nest, Thing plant) => nest.Territory.IsNestRoomCell(plant.Position);

        // 둥지 몫 비료에서 다른 구성원이 지금 주러 가는 몫(그 식물을 가득 채울 만큼)을 뺀 것.
        // 장부는 들고 가는 비료도 세므로, 빼지 않으면 여럿이 한꺼번에 바깥 식물로 가 함께 예비분 아래로 내린다.
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

        // 둥지 세력의 마법식물 중 저장 마나 비율이 가장 낮은 것(플레이어 식물은 세력이 달라 보지 않는다).
        // 이 비비가 줄 수 있는 방 안 식물이 있으면 그중에서만 고르고(다른 비비가 주러 간 식물은 예약으로 빠진다),
        // 바깥 식물은 그런 식물이 없고 주고 나서도 예비분이 남을 때만 고른다.
        private static ArcanePlant FindPlant(Pawn pawn, LordJob_ViviNest nest, bool indoorOnly, int availableFertilizer)
        {
            ArcanePlant indoorBest = null, outdoorBest = null;
            var reserve = OutdoorReserve(nest);
            var indoorBestPct = LowManaPct;
            var outdoorBestPct = LowManaPct;
            var buildings = pawn.Map.listerBuildings.allBuildingsNonColonist;
            for (int i = 0; i < buildings.Count; ++i)
            {
                if (!(buildings[i] is ArcanePlant plant) || plant.Faction != nest.Faction) { continue; }

                // 새싹(저장 칸 없음, 영양은 연료 보충이 채운다)·영원꽃처럼 마나를 저장하지 않는 것과 무한 마나는 뺀다.
                var mana = plant.ManaComp;
                if (mana == null || mana.Props.manaCapacity <= 0f || mana.IsUnlimited) { continue; }

                var indoor = IsIndoorPlant(nest, plant);
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
