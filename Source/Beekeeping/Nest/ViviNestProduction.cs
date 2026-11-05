using RimWorld;
using System.Collections.Generic;
using Verse;
using Verse.AI;

namespace VVRace
{
    // 둥지의 채집 장소와 바닥 작업 자리를 두고, 비축 목표에 맞춰 지시서를 멈추고 푼다.
    // 채집·제작 자체는 기존 채집 지시서와 바닐라 작업 지시서를 그대로 쓴다.
    // 무엇을 먼저 할지는 보유량이 목표에 비해 모자란 정도(1 - 보유/목표)로 정한다: 지시서는 모자란 것부터 오게 다시 늘어놓고,
    // 채집·나무 베기·땅 비료·제작 일은 바닐라 우선도 정렬 노드가 이 값으로 고른다.
    public class ViviNestProduction : IExposable
    {
        public const int RefreshInterval = 600;

        public const int HoneyCapacity = 600;
        // 겨울(바깥 평균 기온이 이보다 낮은 1/12년)에는 꿀 꽃이 얼어 죽어 모은 식량으로만 버틴다.
        public const float WinterTemperature = 0f;
        public const int PollenTarget = 60;
        public const int PropolisTarget = 20;
        public const int CreamTarget = 60;
        public const int WaxTarget = 80;
        public const int WoodTarget = 60;
        // 마나 비료 비축 목표 = 둥지가 하루에 쓰는 비료 × 비축 일수(최소·최대 사이) + 둥지 청사진·골조에 들어갈 양.
        // 장기 계측(툰드라 60일, 목표 60 고정): 바깥 새싹(하루 10개)이 9~12그루로 늘어 방출기(하루 8개)와 함께 하루 100개 넘게 쓰는 동안 둥지 비료가 세 번 0이 됐다.
        // 비축 일수 5일은 새싹이 다 자라는 날 수(5~6.5일)라, 땅을 못 파는 독성 낙진(2.5~10.5일)·유독 연무(1~2일) 동안(바깥에 새 씨앗도 심지 않는다) 남은 새싹을 거의 끝까지 키운다.
        // 최소 120은 방출기만으로 15일(독성 낙진 최장보다 길다), 최대 300은 비료 두 더미(150개씩)라 보관 칸을 크게 차지하지 않고 땅 파기(한 번에 10개)가 다른 일을 오래 밀어내지 않을 만큼이다.
        public const int MinFertilizerTarget = 120;
        public const int MaxFertilizerTarget = 300;
        public const float FertilizerReserveDays = 5f;
        public const int CakeTarget = 40;
        public const int MedicineTarget = 10;
        public const int ClothForCostume = 50;
        public const int CreamReserveForWax = 30;
        // 크림은 비비의 열량을 쓰므로 둥지 식량이 이만큼(일) 남았을 때만 만든다.
        public const float MinFoodDaysForCream = 2f;
        // 가공(꿀 거르기·경단)은 남는 시간 일이라 모자란 정도에 이만큼만 친다(목표의 70% 넘게 찬 자원보다만 앞선다).
        public const float ProcessingWeight = 0.3f;
        // 채집 장소 둘레에서 플레이어 집 구역을 지우는 반경(바닐라 놓기가 같은 더미를 찾아 합치는 범위보다 넉넉히)
        private const float GatherSpotClearHomeRadius = 4f;
        // 생산 주기마다 최근 마법식물에 준 비료에 곱해 비축 일수 동안의 지수 평균으로 만든다.
        private static readonly float RecentPlantFertilizerDecay = UnityEngine.Mathf.Exp(-RefreshInterval / (FertilizerReserveDays * GenDate.TicksPerDay));

        private readonly LordJob_ViviNest _owner;

        private Building_GatherWorkTable _gatherSpot;
        private Building_WorkTable _workSpot;

        // 생산 주기마다 다시 잰 모자란 정도(0~1)
        private readonly Dictionary<RecipeDef, float> _billNeed = new Dictionary<RecipeDef, float>();
        private float _woodNeed;
        private float _fertilizerNeed;
        private float _medicineNeed;
        private int _fertilizerTarget = MinFertilizerTarget;
        // 최근 마법식물에 준 비료(지수 감쇠 합). 지난 비축 일수 동안 준 양이 남으므로 앞으로 비축 일수 동안 줄 양으로 본다.
        private float _recentPlantFertilizer;
        // 타일 기후로 정해지므로 저장하지 않고 처음 쓸 때 잰다.
        private int _winterDays = -1;

        public ViviNestProduction(LordJob_ViviNest owner)
        {
            _owner = owner;
        }

        private Map Map => _owner.Map;
        private ViviNestStock Stock => _owner.Stock;

        public Building_GatherWorkTable GatherSpot => _gatherSpot != null && _gatherSpot.Spawned ? _gatherSpot : null;
        public Building_WorkTable WorkSpot => _workSpot != null && _workSpot.Spawned ? _workSpot : null;

        public float WoodNeed => _woodNeed;
        public float FertilizerNeed => _fertilizerNeed;
        public float MedicineNeed => _medicineNeed;
        public int FertilizerTarget => _fertilizerTarget;

        public void Tick()
        {
            EnsureGatherSpot();
            EnsureWorkSpot();
            UpdateFertilizerTarget();
            UpdateBills();
        }

        // 간이 채집 장소(작업량 0)는 바닐라 설계자처럼 바로 세운다. 문 밖 가까운 빈 땅 중 채집 반경 안 꿀 꽃이 가장 많은 칸.
        private void EnsureGatherSpot()
        {
            if (GatherSpot != null) { return; }

            var best = IntVec3.Invalid;
            var bestFlowers = -1;
            foreach (var cell in _owner.Territory.OutdoorStorageCells)
            {
                if (!CanPlaceSpot(VVThingDefOf.VV_GatheringSpot, cell, requireRoom: false)) { continue; }

                var flowers = CountHoneyFlowers(cell, Building_GatherWorkTable.GatherMaxRadius);
                if (flowers > bestFlowers)
                {
                    best = cell;
                    bestFlowers = flowers;
                }
            }

            if (best.IsValid)
            {
                var cell = best;
                var spot = (Building_GatherWorkTable)ThingMaker.MakeThing(VVThingDefOf.VV_GatheringSpot);
                spot.SetFaction(_owner.Faction);
                GenSpawn.Spawn(spot, cell, Map, Rot4.South);
                spot.GatherRadius = Building_GatherWorkTable.GatherMaxRadius;

                AddBill(spot, VVRecipeDefOf.VV_GatherHoney);
                AddBill(spot, VVRecipeDefOf.VV_GatherPollen);
                AddBill(spot, VVRecipeDefOf.VV_GatherPropolis);

                _gatherSpot = spot;
                ClearHomeAreaAround(cell);
            }
        }

        // 정착민은 집 구역의 오물만 치운다. 채집 장소 둘레에서 정착민이 꽃가루 오물을 치우면 금지 없는 꽃가루 더미가 생기고,
        // 바닐라 놓기는 산출물을 근처 같은 더미에 합치며 합친 더미는 그 더미의 금지 상태를 따르므로 둥지 꽃가루가 계속 사라졌다(자동 테스트, 475개 더미).
        // 그래서 정착 때 방 안 플레이어 구역을 지우듯 채집 장소 둘레의 집 구역을 지운다.
        private void ClearHomeAreaAround(IntVec3 center)
        {
            var map = Map;
            var home = map.areaManager.Home;
            var cellCount = GenRadial.NumCellsInRadius(GatherSpotClearHomeRadius);
            for (int i = 0; i < cellCount; ++i)
            {
                var cell = center + GenRadial.RadialPattern[i];
                if (cell.InBounds(map) && home[cell]) { home[cell] = false; }
            }
        }

        private int CountHoneyFlowers(IntVec3 center, float radius)
        {
            var map = Map;
            var count = 0;
            var cellCount = GenRadial.NumCellsInRadius(radius);
            for (int i = 0; i < cellCount; ++i)
            {
                var cell = center + GenRadial.RadialPattern[i];
                if (cell.InBounds(map) && ViviNestSiteEvaluator.IsHoneyFlower(cell.GetPlant(map)))
                {
                    count++;
                }
            }
            return count;
        }

        // 바닥 작업 자리는 방 안, 상호작용 칸까지 방 안에 들어가는 빈 칸에 둔다.
        private void EnsureWorkSpot()
        {
            if (WorkSpot != null) { return; }

            foreach (var room in _owner.Territory.Rooms)
            {
                foreach (var cell in room.Cells)
                {
                    if (!CanPlaceSpot(VVThingDefOf.VV_ViviNestWorkSpot, cell, requireRoom: true)) { continue; }

                    var spot = (Building_WorkTable)ThingMaker.MakeThing(VVThingDefOf.VV_ViviNestWorkSpot);
                    spot.SetFaction(_owner.Faction);
                    GenSpawn.Spawn(spot, cell, Map, Rot4.South);

                    // 처음 순서. 생산 주기마다 모자란 것부터 오게 다시 늘어놓는다(같으면 이 순서).
                    AddBill(spot, VVRecipeDefOf.Make_VV_BasicWorkerCostume);
                    AddBill(spot, VVRecipeDefOf.Make_VV_Slingshot);
                    AddBill(spot, VVRecipeDefOf.VV_MakeVivicream);
                    AddBill(spot, VVRecipeDefOf.VV_MakeViviwax);
                    AddBill(spot, VVRecipeDefOf.VV_FilteringHoney);
                    AddBill(spot, VVRecipeDefOf.VV_Make_HoneyPollenCake);

                    _workSpot = spot;
                    return;
                }
            }
        }

        private bool CanPlaceSpot(ThingDef def, IntVec3 cell, bool requireRoom)
        {
            var map = Map;
            var interaction = ThingUtility.InteractionCellWhenAt(def, cell, Rot4.South, map);
            foreach (var c in new[] { cell, interaction })
            {
                if (!c.InBounds(map) || !c.Standable(map)) { return false; }
                if (requireRoom ? !_owner.Territory.IsRoomCell(c) : _owner.Territory.IsRoomCell(c)) { return false; }
                if (c.GetEdifice(map) != null) { return false; }
                foreach (var thing in c.GetThingList(map))
                {
                    if (thing is Building || thing is Pawn || thing.def.IsBlueprint || thing.def.IsFrame) { return false; }
                    if (thing.def.category == ThingCategory.Item) { return false; }
                }
                foreach (var door in _owner.Territory.BorderDoors)
                {
                    if (c.AdjacentTo8WayOrInside(door.Position)) { return false; }
                }
            }

            return true;
        }

        private static void AddBill(Building_WorkTable table, RecipeDef recipe)
        {
            if (recipe == null) { return; }

            var bill = recipe.MakeNewBill();
            if (bill is Bill_Production production)
            {
                production.repeatMode = BillRepeatModeDefOf.Forever;
                production.SetStoreMode(BillStoreModeDefOf.DropOnFloor);
            }
            table.BillStack.AddBill(bill);
        }

        private static Bill FindBill(Building_WorkTable table, RecipeDef recipe)
        {
            if (table == null) { return null; }

            foreach (var bill in table.BillStack)
            {
                if (bill.recipe == recipe) { return bill; }
            }
            return null;
        }

        private static void SetActive(Building_WorkTable table, RecipeDef recipe, bool active)
        {
            var bill = FindBill(table, recipe);
            if (bill != null)
            {
                // 재료가 미완성품에 들어가 재고가 줄어도, 만들던 비비가 끝낼 수 있게 그동안은 멈추지 않는다.
                bill.suspended = !active && !(bill is Bill_ProductionWithUft uftBill && uftBill.BoundWorker != null);
            }
        }

        private void UpdateBills()
        {
            var stock = Stock;
            var rawHoney = stock.CountOf(VVThingDefOf.VV_RawHoney);
            var filteredHoney = stock.CountOf(VVThingDefOf.VV_FilteredHoney);
            var pollen = stock.CountOf(VVThingDefOf.VV_Pollen);
            var propolis = stock.CountOf(VVThingDefOf.VV_Propolis);
            var cream = stock.CountOf(VVThingDefOf.VV_Vivicream);
            var wax = stock.CountOf(VVThingDefOf.VV_Viviwax);
            var wood = stock.CountOf(ThingDefOf.WoodLog);
            var cloth = stock.CountOf(ThingDefOf.Cloth);
            var cakes = stock.CountOf(VVThingDefOf.VV_HoneyPollenCake);

            var gather = GatherSpot;
            var honeyTarget = HoneyTarget();
            SetActive(gather, VVRecipeDefOf.VV_GatherHoney, rawHoney + filteredHoney < honeyTarget);
            SetActive(gather, VVRecipeDefOf.VV_GatherPollen, pollen < PollenTarget);
            SetActive(gather, VVRecipeDefOf.VV_GatherPropolis, propolis < PropolisTarget);

            var work = WorkSpot;
            SetActive(work, VVRecipeDefOf.VV_MakeVivicream, cream < Target(VVThingDefOf.VV_Vivicream, CreamTarget) && FoodReserveDays() >= MinFoodDaysForCream);
            SetActive(work, VVRecipeDefOf.VV_MakeViviwax, wax < Target(VVThingDefOf.VV_Viviwax, WaxTarget) && cream >= CreamReserveForWax);
            SetActive(work, VVRecipeDefOf.Make_VV_BasicWorkerCostume, cloth >= ClothForCostume && AnyMemberNeedsCostume());
            SetActive(work, VVRecipeDefOf.Make_VV_Slingshot, wood >= 20 && wax >= 10 && AnySlingerNeedsWeapon());
            SetActive(work, VVRecipeDefOf.VV_FilteringHoney, rawHoney >= 20);
            SetActive(work, VVRecipeDefOf.VV_Make_HoneyPollenCake, cakes < CakeTarget && filteredHoney >= 3 && pollen >= 6);

            // 장비(옷·새총)는 필요하면 가장 먼저, 꿀은 둥지 식량이 모자라면 가장 먼저 한다.
            var honey = rawHoney + filteredHoney;
            _billNeed[VVRecipeDefOf.VV_GatherHoney] = FoodReserveDays() < MinFoodDaysForCream ? 1f : Shortage(honey, honeyTarget);
            _billNeed[VVRecipeDefOf.VV_GatherPollen] = Shortage(pollen, PollenTarget);
            _billNeed[VVRecipeDefOf.VV_GatherPropolis] = Shortage(propolis, PropolisTarget);
            _billNeed[VVRecipeDefOf.Make_VV_BasicWorkerCostume] = 1f;
            _billNeed[VVRecipeDefOf.Make_VV_Slingshot] = 1f;
            _billNeed[VVRecipeDefOf.VV_MakeVivicream] = Shortage(cream, Target(VVThingDefOf.VV_Vivicream, CreamTarget));
            _billNeed[VVRecipeDefOf.VV_MakeViviwax] = Shortage(wax, Target(VVThingDefOf.VV_Viviwax, WaxTarget));
            _billNeed[VVRecipeDefOf.VV_FilteringHoney] = ProcessingWeight * (honey > 0 ? (float)rawHoney / honey : 0f);
            _billNeed[VVRecipeDefOf.VV_Make_HoneyPollenCake] = ProcessingWeight * Shortage(cakes, CakeTarget);
            _woodNeed = Shortage(wood, Target(ThingDefOf.WoodLog, WoodTarget));
            _fertilizerNeed = Shortage(stock.CountOf(VVThingDefOf.VV_Fertilizer), _fertilizerTarget);
            _medicineNeed = Shortage(stock.CountOf(ThingDefOf.MedicineHerbal) + stock.CountOf(ThingDefOf.MedicineIndustrial) + stock.CountOf(ThingDefOf.MedicineUltratech), MedicineTarget);

            SortBills(gather);
            SortBills(work);
        }

        private static float Shortage(int have, int target) => target > 0 ? UnityEngine.Mathf.Clamp01(1f - (float)have / target) : 0f;

        // 멈춘 지시서는 0
        public float NeedOf(Bill bill)
        {
            return bill != null && !bill.suspended && _billNeed.TryGetValue(bill.recipe, out var need) ? need : 0f;
        }

        // 바닐라 지시서 워크기버는 위에서부터 고르므로 모자란 것부터 오게 다시 늘어놓는다(삽입 정렬이라 같으면 원래 순서).
        private void SortBills(Building_WorkTable table)
        {
            table?.BillStack.Bills.InsertionSort((a, b) => NeedOf(b).CompareTo(NeedOf(a)));
        }

        public float GatherPriority()
        {
            var spot = GatherSpot;
            if (spot == null) { return 0f; }

            var best = 0f;
            foreach (var bill in spot.BillStack)
            {
                best = UnityEngine.Mathf.Max(best, NeedOf(bill));
            }
            return best;
        }

        // 만들다 만 것은 시작한 비비만 이어서 만들 수 있으므로(바닐라), 그 비비에게는 가장 먼저 할 일이고 다른 비비에게는 없는 일이다.
        public float CraftPriority(Pawn pawn)
        {
            var spot = WorkSpot;
            if (spot == null) { return 0f; }

            var best = 0f;
            foreach (var bill in spot.BillStack)
            {
                if (bill.suspended) { continue; }
                if (bill is Bill_ProductionWithUft uftBill && uftBill.BoundWorker != null)
                {
                    if (uftBill.BoundWorker == pawn) { return 1f; }
                    continue;
                }
                best = UnityEngine.Mathf.Max(best, NeedOf(bill));
            }
            return best;
        }

        public float CraftJobPriority(Pawn pawn, Job job)
        {
            if (job?.bill is Bill_ProductionWithUft uftBill && uftBill.BoundWorker == pawn) { return 1f; }
            return NeedOf(job?.bill);
        }

        // 비축 목표: 기본 목표에 둥지 청사진·골조에 아직 들어가야 할 양을 더한다.
        public int Target(ThingDef def, int baseTarget) => baseTarget + _owner.Planner.ResourceNeeded(def);

        // 마나 비료 비축 목표를 생산 주기마다 다시 잰다(땅 비료·비료 주기 잡은 이 값을 쓴다).
        private void UpdateFertilizerTarget()
        {
            _recentPlantFertilizer *= RecentPlantFertilizerDecay;
            var target = UnityEngine.Mathf.CeilToInt(FertilizerPerDay() * FertilizerReserveDays);
            _fertilizerTarget = Target(VVThingDefOf.VV_Fertilizer, UnityEngine.Mathf.Clamp(target, MinFertilizerTarget, MaxFertilizerTarget));
        }

        // 둥지가 하루에 쓰는 마나 비료: 비료를 태우는 둥지 것(마나 방출기·새싹)의 연료 소모(바닐라 연료 소모율) + 최근 마법식물에 준 양의 하루 평균.
        // 마법식물의 마나 소모 규칙 값은 환경 마나 흡수와 스스로 내는 마나를 빼지 않아 실제로 주는 비료보다 몇 배 크고 시각·기온에 따라 출렁이므로,
        // 둥지 비비가 실제로 준 양을 잰다.
        public float FertilizerPerDay()
        {
            var perDay = _recentPlantFertilizer / FertilizerReserveDays;
            var refuelables = Map.listerThings.ThingsInGroup(ThingRequestGroup.Refuelable);
            for (int i = 0; i < refuelables.Count; ++i)
            {
                if (refuelables[i].Faction != _owner.Faction) { continue; }

                // 쓸 때만 태우는 것(대형 화분 등)은 하루 소모량을 알 수 없어 뺀다.
                var props = refuelables[i].TryGetComp<CompRefuelable>()?.Props;
                if (props == null || props.consumeFuelOnlyWhenUsed || props.externalTicking || !props.fuelFilter.Allows(VVThingDefOf.VV_Fertilizer)) { continue; }

                perDay += props.fuelConsumptionRate / props.FuelMultiplierCurrentDifficulty;
            }
            return perDay;
        }

        public void Notify_PlantFertilized(int count)
        {
            _recentPlantFertilizer += count;
        }

        // 둥지 식량이 며칠 치인지(구성원이 배부를 때 배고파지는 속도 기준)
        public float FoodReserveDays()
        {
            var perDay = FoodPerDay();
            return perDay > 0f ? Stock.FoodNutrition / perDay : float.MaxValue;
        }

        // 구성원이 배부를 때 하루에 먹는 영양
        public float FoodPerDay()
        {
            var pawns = _owner.lord?.ownedPawns;
            var perDay = 0f;
            if (pawns != null)
            {
                for (int i = 0; i < pawns.Count; ++i)
                {
                    var food = pawns[i].needs?.food;
                    if (food != null)
                    {
                        perDay += food.FoodFallPerTickAssumingCategory(HungerCategory.Fed) * GenDate.TicksPerDay;
                    }
                }
            }
            return perDay;
        }

        // 겨울 날 수: 이 타일에서 바깥 평균 기온이 영하인 1/12년(5일)을 모두 센다.
        public int WinterDays()
        {
            if (_winterDays < 0)
            {
                _winterDays = 0;
                for (int i = 0; i < GenDate.TwelfthsPerYear; ++i)
                {
                    if (GenTemperature.AverageTemperatureAtTileForTwelfth(Map.Tile, (Twelfth)i) < WinterTemperature)
                    {
                        _winterDays += GenDate.DaysPerTwelfth;
                    }
                }
            }
            return _winterDays;
        }

        // 꿀(날꿀+걸러낸 꿀) 비축 목표: 평소 600개, 겨울이 있는 땅에서는 겨울 날 수만큼 먹을 걸러낸 꿀.
        // 장기 계측(온대림, 겨울 약 20일): 600개 상한·꽃 40송이로는 겨울 열흘쯤 만에 식량이 떨어져 봄 직전에 아기가 굶어 죽었다.
        public int HoneyTarget()
        {
            var winterDays = WinterDays();
            if (winterDays <= 0) { return HoneyCapacity; }

            var perUnit = VVThingDefOf.VV_FilteredHoney.GetStatValueAbstract(StatDefOf.Nutrition);
            return perUnit > 0f ? UnityEngine.Mathf.Max(HoneyCapacity, UnityEngine.Mathf.CeilToInt(FoodPerDay() * winterDays / perUnit)) : HoneyCapacity;
        }

        public bool AnyMemberNeedsCostume()
        {
            var pawns = _owner.lord?.ownedPawns;
            if (pawns == null) { return false; }

            for (int i = 0; i < pawns.Count; ++i)
            {
                if (NeedsCostume(pawns[i]) && Stock.CountOf(VVThingDefOf.VV_BasicWorkerCostume) == 0) { return true; }
            }
            return false;
        }

        // 어른 일반 비비는 기본옷을 입는다(로열·아이는 처음 입고 온 옷을 그대로 쓴다).
        public static bool NeedsCostume(Pawn pawn)
        {
            if (!pawn.DevelopmentalStage.Adult() || pawn.IsRoyalVivi() || pawn.apparel == null) { return false; }

            foreach (var apparel in pawn.apparel.WornApparel)
            {
                if (apparel.def == VVThingDefOf.VV_BasicWorkerCostume && apparel.HitPoints >= apparel.MaxHitPoints * 0.4f)
                {
                    return false;
                }
            }
            return true;
        }

        public bool AnySlingerNeedsWeapon()
        {
            var pawns = _owner.lord?.ownedPawns;
            if (pawns == null) { return false; }

            for (int i = 0; i < pawns.Count; ++i)
            {
                if (NeedsSlingshot(pawns[i]) && Stock.CountOf(VVThingDefOf.VV_Slingshot) == 0) { return true; }
            }
            return false;
        }

        public static bool NeedsSlingshot(Pawn pawn)
        {
            return pawn.kindDef == VVPawnKindDefOf.VV_WanderingViviSlinger && pawn.DevelopmentalStage.Adult()
                && pawn.equipment != null && pawn.equipment.Primary == null && !pawn.WorkTagIsDisabled(WorkTags.Violent);
        }

        public void ExposeData()
        {
            Scribe_References.Look(ref _gatherSpot, "gatherSpot");
            Scribe_References.Look(ref _workSpot, "workSpot");
            Scribe_Values.Look(ref _recentPlantFertilizer, "recentPlantFertilizer");
        }
    }
}
