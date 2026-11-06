using RimWorld;
using System.Collections.Generic;
using Verse;
using Verse.AI;

namespace VVRace
{
    // 둥지의 바닥 작업 자리들을 두고, 비축 목표에 맞춰 지시서를 멈추고 푼다. 제작 자체는 바닐라 작업 지시서를 그대로 쓴다.
    // 채집은 채집 장소·지시서 없이 채집 잡 기버가 둥지 채집 범위의 꽃·나무에서 그 자리에서 한다(JobGiver_ViviNestGather).
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
        // 원목 비축 목표 = 기본 목표 + 원목을 태우는 둥지 냉각기의 하루 사용량 × 비축 일수(비료와 같은 5일) + 둥지 청사진·골조에 들어갈 양(WoodStockTarget).
        // 냉각기는 원목 하루 10개(가득 50 = 5일)를 태우므로 하나면 목표가 60 → 110이 된다. 서늘한 철에 채우지 않는 냉각기(ViviNestPlanner.NeedsCooling)는 세지 않는다.
        public const float WoodReserveDays = FertilizerReserveDays;
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
        // 일하는 구성원(아기 제외) 이만큼마다 바닥 작업 자리를 하나 둔다(올림, 사용자 요구).
        public const int WorkersPerWorkSpot = 2;
        // 둥지 채집 범위: 둥지 중심에서 기존 채집 장소의 최대 채집 반경까지. 꽃 세기·꽃 심기·바깥 마법식물 자리도 이 범위를 쓴다.
        public const float GatherRadius = Building_GatherWorkTable.GatherMaxRadius;
        // 그 자리에서 채취할 때의 채집량 배율. 예전 둥지 채집 자리(간이 채집 장소와 같은 WorkTableEfficiencyFactor 0.5)와 같게 둔다.
        // 채집 자리를 오가는 걸음이 없어져 일손은 덜 들지만, 둥지 식량은 일손이 아니라 꿀 꽃 수와 재채집 대기로 정해진다
        // (자동 테스트: 민들레 재채집 대기 약 4.2일, 다섯 마리에 22송이는 하루 −2, 40송이가 본전; 장기 계측에서 채집 자리 채집은 일과의 5%이고 편자 놀이·산책이 30%를 넘었다).
        // 그 자리 채집으로 바꾼 뒤 장기 계측(온대·한랭 60일)에서 채집은 일과의 2~4%였고, 둥지 식량은 정착 5일 뒤로 7.7일 치 아래로 내려가지 않았다.
        // 배율을 올리면 꽃 목표(ViviNestGarden)와 겨울 비축을 다시 재야 한다.
        public const float GatherYieldFactor = 0.5f;
        // 생산 주기마다 최근 마법식물에 준 비료에 곱해 비축 일수 동안의 지수 평균으로 만든다.
        private static readonly float RecentPlantFertilizerDecay = UnityEngine.Mathf.Exp(-RefreshInterval / (FertilizerReserveDays * GenDate.TicksPerDay));

        private readonly LordJob_ViviNest _owner;

        private List<Building_WorkTable> _workSpots = new List<Building_WorkTable>();

        private static List<RecipeDef> _workRecipes;
        private static readonly List<RecipeDef> _tmpRecipes = new List<RecipeDef>();
        private static readonly List<int> _tmpSpotLoad = new List<int>();
        private static readonly List<int> _tmpChosen = new List<int>();

        // 생산 주기마다 다시 잰 레시피별 모자란 정도(0~1). 작업 지시서와 채집 잡 기버(채집 레시피, 지시서 없음)가 쓴다.
        private readonly Dictionary<RecipeDef, float> _billNeed = new Dictionary<RecipeDef, float>();
        private float _woodNeed;
        private float _fertilizerNeed;
        private float _medicineNeed;
        private int _fertilizerTarget = MinFertilizerTarget;
        // 원목 비축 목표(생산 주기마다 잰다, -1이면 아직 재지 않음). 나무 베기 잡 기버가 사고마다 다시 재면 맵 전체 연료 칸을 훑었다.
        private int _woodStockTarget = -1;
        // 최근 마법식물에 준 비료(지수 감쇠 합). 지난 비축 일수 동안 준 양이 남으므로 앞으로 비축 일수 동안 줄 양으로 본다.
        private float _recentPlantFertilizer;
        // 타일 기후로 정해지므로 저장하지 않고 처음 쓸 때 잰다.
        private int _winterDays = -1;

        // 작업 자리의 상호작용 칸(저장하지 않는다). 바깥 빈 땅·가구 자리 검사가 칸마다 자리 목록을 훑고 상호작용 칸을 다시 계산하던 것을 묶어 둔다.
        // 자리를 두거나 치울 때와 맵의 둥지 작업 자리가 없어질 때(둥지 토일 알림) 다시 만든다.
        private readonly HashSet<IntVec3> _spotInteractionCells = new HashSet<IntVec3>();
        private bool _spotInteractionCellsDirty = true;

        // "지금 할 수 없음" 기억(저장하지 않는다): 대상을 못 찾은 생산 일은 대상이 생길 수 있는 다음 틱까지 우선도 0으로 둔다.
        // 바닐라 우선도 정렬은 우선도가 0보다 크면 실패해도 사고마다 TryGiveJob을 부르므로(실측, 온대 11명: 약뿌리 탐색은 늘 실패하며 1회 17~30 µs,
        // 건조 맵 채집 실패 39 µs, 나무 베기 13.7 µs) 늘 실패하는 탐색이 사고마다 되풀이됐다.
        // 둥지 단위로는 그 비비와 상관없이 대상이 없을 때만 기억한다(예약·도달·기술처럼 그 비비 탓인 실패는 기억하지 않는다).
        // 다음 틱은 대상 데이터에서 구하고(재채집 대기 끝, 지금 성장 속도로 거둘 만큼 자랄 때), 구할 수 없는 경우를 위해 상한을 둔다.
        public const int HealrootRetryMaxTicks = 1250;
        public const int TreeRetryMaxTicks = 1250;
        public const int GatherRetryMaxTicks = 600;
        private int _healrootRetryTick = -1;
        private int _treeRetryTick = -1;
        private readonly Dictionary<RecipeDef, int> _gatherRetryTicks = new Dictionary<RecipeDef, int>();

        // 구성원별 채집 스탯 판정(레시피의 작업 속도·채집량 스탯 > 0, 저장하지 않는다). 폰 스탯은 캐시 없이 계산하면 레시피 하나에 2~20 µs였다.
        public const int GatherStatsCacheTicks = 2500;
        private readonly Dictionary<(Pawn, RecipeDef), (int tick, bool ok)> _gatherStats = new Dictionary<(Pawn, RecipeDef), (int tick, bool ok)>();
        private static readonly List<(Pawn, RecipeDef)> _tmpStaleGatherStats = new List<(Pawn, RecipeDef)>();

        public ViviNestProduction(LordJob_ViviNest owner)
        {
            _owner = owner;
        }

        private Map Map => _owner.Map;
        private ViviNestStock Stock => _owner.Stock;

        // 둥지 채집 범위의 중심(둥지 방 한가운데 칸)
        public IntVec3 GatherCenter => _owner.NestCell;
        public bool InGatherRange(IntVec3 cell) => cell.InHorDistOf(GatherCenter, GatherRadius);
        // 바닥 작업 자리들. 사라진 자리는 생산 주기마다 빼므로 그사이에는 Spawned를 보고 쓴다.
        public IReadOnlyList<Building_WorkTable> WorkSpots => _workSpots;

        // 작업 자리 지시서(처음 순서). 생산 주기마다 모자란 것부터 오게 다시 늘어놓는다(같으면 이 순서).
        private static List<RecipeDef> WorkRecipes
        {
            get
            {
                if (_workRecipes == null)
                {
                    _workRecipes = new List<RecipeDef>
                    {
                        VVRecipeDefOf.Make_VV_BasicWorkerCostume,
                        VVRecipeDefOf.Make_VV_Slingshot,
                        VVRecipeDefOf.VV_MakeVivicream,
                        VVRecipeDefOf.VV_MakeViviwax,
                        VVRecipeDefOf.VV_FilteringHoney,
                        VVRecipeDefOf.VV_Make_HoneyPollenCake,
                    };
                }
                return _workRecipes;
            }
        }

        public float WoodNeed => _woodNeed;
        public float FertilizerNeed => _fertilizerNeed;
        public float MedicineNeed => _medicineNeed;
        public int FertilizerTarget => _fertilizerTarget;

        // 나무 베기 잡 기버가 쓰는 원목 비축 목표: 생산 주기마다 잰 값(아직 재지 않았으면 지금 잰다)
        public int CachedWoodStockTarget => _woodStockTarget >= 0 ? _woodStockTarget : WoodStockTarget();

        public bool HealrootWaiting => Find.TickManager.TicksGame < _healrootRetryTick;
        public bool TreeWaiting => Find.TickManager.TicksGame < _treeRetryTick;
        public bool GatherWaiting(RecipeDef recipe) => _gatherRetryTicks.TryGetValue(recipe, out var tick) && Find.TickManager.TicksGame < tick;

        // 대상이 없을 때 다음에 볼 틱을 기억한다. readyTick은 대상 데이터로 구한 가장 이른 준비 틱(모르면 -1)이고 상한을 넘지 않는다.
        public void Notify_NoHealroot(int readyTick) => _healrootRetryTick = RetryTick(readyTick, HealrootRetryMaxTicks);
        public void Notify_NoTree(int readyTick) => _treeRetryTick = RetryTick(readyTick, TreeRetryMaxTicks);
        public void Notify_NoGatherTarget(RecipeDef recipe, int readyTick) => _gatherRetryTicks[recipe] = RetryTick(readyTick, GatherRetryMaxTicks);

        private static int RetryTick(int readyTick, int maxTicks)
        {
            var max = Find.TickManager.TicksGame + maxTicks;
            return readyTick >= 0 && readyTick < max ? readyTick : max;
        }

        public bool TryGetGatherStats(Pawn pawn, RecipeDef recipe, out bool ok)
        {
            if (_gatherStats.TryGetValue((pawn, recipe), out var entry) && Find.TickManager.TicksGame - entry.tick < GatherStatsCacheTicks)
            {
                ok = entry.ok;
                return true;
            }

            ok = false;
            return false;
        }

        public void SetGatherStats(Pawn pawn, RecipeDef recipe, bool ok) => _gatherStats[(pawn, recipe)] = (Find.TickManager.TicksGame, ok);

        private void PruneGatherStats()
        {
            var ticks = Find.TickManager.TicksGame;
            _tmpStaleGatherStats.Clear();
            foreach (var entry in _gatherStats)
            {
                if (ticks - entry.Value.tick >= GatherStatsCacheTicks) { _tmpStaleGatherStats.Add(entry.Key); }
            }
            for (int i = 0; i < _tmpStaleGatherStats.Count; ++i)
            {
                _gatherStats.Remove(_tmpStaleGatherStats[i]);
            }
            _tmpStaleGatherStats.Clear();
        }

        // 작업 자리의 상호작용 칸인지(바깥 빈 땅·가구 자리를 고를 때 피한다)
        public bool IsWorkSpotInteractionCell(IntVec3 cell)
        {
            if (_spotInteractionCellsDirty)
            {
                _spotInteractionCellsDirty = false;
                _spotInteractionCells.Clear();
                for (int i = 0; i < _workSpots.Count; ++i)
                {
                    if (_workSpots[i].Spawned) { _spotInteractionCells.Add(_workSpots[i].InteractionCell); }
                }
            }
            return _spotInteractionCells.Contains(cell);
        }

        // 맵의 건물이 서거나 없어졌다(둥지 토일 알림). 작업 자리면 상호작용 칸을 다시 만든다.
        public void Notify_BuildingChanged(Building building)
        {
            if (building.def == VVThingDefOf.VV_ViviNestWorkSpot) { _spotInteractionCellsDirty = true; }
        }

        public void Tick()
        {
            EnsureWorkSpots();
            UpdateFertilizerTarget();
            UpdateBills();
            PruneGatherStats();
        }

        // 바닥 작업 자리는 일하는 구성원(아기 제외) 두 마리당 하나(올림)를 둔다(사용자 요구). 한 자리에서는 한 마리만 일하므로(바닐라 지시서 워크기버가 자리를 예약한다)
        // 자리가 하나면 구성원이 늘어도 제작은 한 번에 하나뿐이다. 구성원이 줄면 쓰고 있지 않은(예약·만들다 만 것이 없는) 나중 자리부터 치운다.
        private void EnsureWorkSpots()
        {
            _spotInteractionCellsDirty = true;
            _workSpots.RemoveAll(s => s == null || !s.Spawned);

            var target = WorkSpotTarget();
            while (_workSpots.Count < target && TryPlaceWorkSpot()) { }

            for (int i = _workSpots.Count - 1; i >= 0 && _workSpots.Count > target; --i)
            {
                var spot = _workSpots[i];
                if (IsWorkSpotInUse(spot)) { continue; }

                _workSpots.RemoveAt(i);
                spot.Destroy();
                _spotInteractionCellsDirty = true;
            }
        }

        public int WorkSpotTarget()
        {
            var pawns = _owner.lord?.ownedPawns;
            var workers = 0;
            if (pawns != null)
            {
                for (int i = 0; i < pawns.Count; ++i)
                {
                    var stage = pawns[i].DevelopmentalStage;
                    if (!stage.Baby() && !stage.Newborn()) { workers++; }
                }
            }
            return UnityEngine.Mathf.Max(1, (workers + WorkersPerWorkSpot - 1) / WorkersPerWorkSpot);
        }

        // 누가 쓰고 있거나(바닐라 지시서 잡은 자리를 예약한다) 만들다 만 것이 걸린 자리
        private bool IsWorkSpotInUse(Building_WorkTable spot)
        {
            if (Map.reservationManager.IsReservedByAnyoneOf(spot, _owner.Faction)) { return true; }
            foreach (var bill in spot.BillStack)
            {
                if (bill is Bill_ProductionWithUft uftBill && uftBill.BoundUft != null) { return true; }
            }
            return false;
        }

        // 방 안, 상호작용 칸까지 방 안에 들어가는 빈 칸(CanPlaceSpot)에 둔다. 다른 자리의 상호작용 칸과 겹치지 않게 하고,
        // 선반·화분·부화기가 쓰는 벽 쪽 칸을 남기려고 자리와 상호작용 칸이 벽에 붙지 않은 칸을 먼저 쓴다(없으면 벽 쪽 칸).
        private bool TryPlaceWorkSpot()
        {
            var def = VVThingDefOf.VV_ViviNestWorkSpot;
            var fallback = IntVec3.Invalid;
            foreach (var room in _owner.Territory.Rooms)
            {
                foreach (var cell in room.Cells)
                {
                    if (!CanPlaceSpot(def, cell)) { continue; }

                    var interaction = ThingUtility.InteractionCellWhenAt(def, cell, Rot4.South, Map);
                    if (IsWorkSpotInteractionCell(cell) || IsWorkSpotInteractionCell(interaction)) { continue; }

                    if (!TouchesWall(cell) && !TouchesWall(interaction))
                    {
                        PlaceWorkSpot(cell);
                        return true;
                    }
                    if (!fallback.IsValid) { fallback = cell; }
                }
            }

            if (!fallback.IsValid) { return false; }
            PlaceWorkSpot(fallback);
            return true;
        }

        private bool TouchesWall(IntVec3 cell)
        {
            var map = Map;
            foreach (var dir in GenAdj.CardinalDirections)
            {
                var adj = cell + dir;
                var edifice = adj.InBounds(map) ? adj.GetEdifice(map) : null;
                if (edifice != null && edifice.def.passability == Traversability.Impassable) { return true; }
            }
            return false;
        }

        private void PlaceWorkSpot(IntVec3 cell)
        {
            var spot = (Building_WorkTable)ThingMaker.MakeThing(VVThingDefOf.VV_ViviNestWorkSpot);
            spot.SetFaction(_owner.Faction);
            GenSpawn.Spawn(spot, cell, Map, Rot4.South);

            foreach (var recipe in WorkRecipes)
            {
                AddBill(spot, recipe);
            }
            _workSpots.Add(spot);
            _spotInteractionCellsDirty = true;
        }

        // 자리 칸과 상호작용 칸이 모두 쓸 수 있는 방 칸
        private bool CanPlaceSpot(ThingDef def, IntVec3 cell)
        {
            var interaction = ThingUtility.InteractionCellWhenAt(def, cell, Rot4.South, Map);
            return CanPlaceSpotCell(cell) && CanPlaceSpotCell(interaction);
        }

        private bool CanPlaceSpotCell(IntVec3 c)
        {
            var map = Map;
            if (!c.InBounds(map) || !c.Standable(map)) { return false; }
            if (!_owner.Territory.IsRoomCell(c)) { return false; }
            if (c.GetEdifice(map) != null) { return false; }
            foreach (var thing in c.GetThingList(map))
            {
                if (thing is Building || thing is Pawn || thing.def.IsBlueprint || thing.def.IsFrame) { return false; }
                if (thing.def.category == ThingCategory.Item) { return false; }
            }
            return !_owner.Territory.IsBesideBorderDoor(c);
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
            var cakes = stock.CountOf(VVThingDefOf.VV_HoneyPollenCake);

            // 채집 레시피의 모자란 정도는 채집 잡 기버가 고를 때 쓴다(목표에 닿은 자원은 0). 꿀은 둥지 식량이 모자라면 가장 먼저 하고,
            // 장비(옷·새총)는 필요하면 가장 먼저 한다.
            var honeyTarget = HoneyTarget();
            var honey = rawHoney + filteredHoney;
            _billNeed[VVRecipeDefOf.VV_GatherHoney] = honey >= honeyTarget ? 0f : (FoodReserveDays() < MinFoodDaysForCream ? 1f : Shortage(honey, honeyTarget));
            _billNeed[VVRecipeDefOf.VV_GatherPollen] = Shortage(pollen, PollenTarget);
            _billNeed[VVRecipeDefOf.VV_GatherPropolis] = Shortage(propolis, PropolisTarget);
            _billNeed[VVRecipeDefOf.Make_VV_BasicWorkerCostume] = 1f;
            _billNeed[VVRecipeDefOf.Make_VV_Slingshot] = 1f;
            _billNeed[VVRecipeDefOf.VV_MakeVivicream] = Shortage(cream, Target(VVThingDefOf.VV_Vivicream, CreamTarget));
            _billNeed[VVRecipeDefOf.VV_MakeViviwax] = Shortage(wax, Target(VVThingDefOf.VV_Viviwax, WaxTarget));
            _billNeed[VVRecipeDefOf.VV_FilteringHoney] = ProcessingWeight * (honey > 0 ? (float)rawHoney / honey : 0f);
            _billNeed[VVRecipeDefOf.VV_Make_HoneyPollenCake] = ProcessingWeight * Shortage(cakes, CakeTarget);
            _woodStockTarget = WoodStockTarget();
            _woodNeed = Shortage(wood, _woodStockTarget);
            _fertilizerNeed = Shortage(stock.CountOf(VVThingDefOf.VV_Fertilizer), _fertilizerTarget);
            _medicineNeed = Shortage(stock.CountOf(ThingDefOf.MedicineHerbal) + stock.CountOf(ThingDefOf.MedicineIndustrial) + stock.CountOf(ThingDefOf.MedicineUltratech), MedicineTarget);

            AllocateWorkBills();

            for (int i = 0; i < _workSpots.Count; ++i)
            {
                SortBills(_workSpots[i]);
            }
        }

        // 작업 자리마다 같은 지시서를 두되, 한 지시를 켜 두는 자리 수는 그 지시로 아직 만들 묶음 수(WantedBatches)까지만이다.
        // 한 자리에서는 한 마리만 일하므로 켠 자리 수가 그 지시를 동시에 만드는 수의 상한이 된다(자리마다 따로 켜면 목표를 넘겨 만든다).
        // 지금 만들고 있는 자리는 그대로 켜 두고(멈추면 바닐라가 하던 일을 끊는다) 묶음 수에서 빼며, 나머지 묶음은 모자란 지시부터 켠 지시서가 적은 자리에 나눠 켜서
        // 여러 자리가 함께 일하게 한다. 만들 것이 없으면 자리가 하나일 때처럼 모두 멈추되, 만들다 만 것이 걸린 지시서는 그 비비가 끝낼 수 있게 둔다.
        private void AllocateWorkBills()
        {
            var spots = _workSpots;
            _tmpSpotLoad.Clear();
            for (int i = 0; i < spots.Count; ++i)
            {
                var load = 0;
                foreach (var bill in spots[i].BillStack)
                {
                    if (InProgress(bill)) { load++; }
                }
                _tmpSpotLoad.Add(load);
            }

            _tmpRecipes.Clear();
            _tmpRecipes.AddRange(WorkRecipes);
            _tmpRecipes.InsertionSort((a, b) => Need(b).CompareTo(Need(a)));

            foreach (var recipe in _tmpRecipes)
            {
                var wanted = WantedBatches(recipe);
                _tmpChosen.Clear();
                if (wanted > 0)
                {
                    for (int i = 0; i < spots.Count; ++i)
                    {
                        var bill = FindBill(spots[i], recipe);
                        if (bill != null && InProgress(bill)) { _tmpChosen.Add(i); }
                    }
                    for (var slots = wanted - _tmpChosen.Count; slots > 0; --slots)
                    {
                        var best = -1;
                        for (int i = 0; i < spots.Count; ++i)
                        {
                            if (_tmpChosen.Contains(i) || FindBill(spots[i], recipe) == null) { continue; }
                            if (best < 0 || _tmpSpotLoad[i] < _tmpSpotLoad[best]) { best = i; }
                        }
                        if (best < 0) { break; }

                        _tmpChosen.Add(best);
                        _tmpSpotLoad[best]++;
                    }
                }

                for (int i = 0; i < spots.Count; ++i)
                {
                    var bill = FindBill(spots[i], recipe);
                    if (bill != null)
                    {
                        // 재료가 미완성품에 들어가 재고가 줄어도, 만들던 비비가 끝낼 수 있게 그동안은 멈추지 않는다.
                        bill.suspended = !_tmpChosen.Contains(i) && !(bill is Bill_ProductionWithUft uftBill && uftBill.BoundWorker != null);
                    }
                }
            }
        }

        public float Need(RecipeDef recipe) => _billNeed.TryGetValue(recipe, out var need) ? need : 0f;

        // 그 지시로 지금 만들어야 할 묶음 수(0이면 멈춘다): 목표까지 모자란 양을 한 번에 나오는 양으로 나눠 올리고, 둥지 몫 재료로 만들 수 있는 묶음 수를 넘지 않는다.
        // 켜는 조건은 자리가 하나일 때와 같다(크림은 식량이 이틀 치 넘을 때, 밀랍은 크림 30마다, 옷·새총은 필요한 비비 수만큼, 꿀 거르기는 날꿀 20마다).
        public int WantedBatches(RecipeDef recipe)
        {
            var stock = Stock;
            var batches = 0;
            if (recipe == VVRecipeDefOf.VV_MakeVivicream)
            {
                var cream = stock.CountOf(VVThingDefOf.VV_Vivicream);
                batches = FoodReserveDays() >= MinFoodDaysForCream ? Batches(recipe, Target(VVThingDefOf.VV_Vivicream, CreamTarget) - cream) : 0;
            }
            else if (recipe == VVRecipeDefOf.VV_MakeViviwax)
            {
                var wax = stock.CountOf(VVThingDefOf.VV_Viviwax);
                batches = UnityEngine.Mathf.Min(Batches(recipe, Target(VVThingDefOf.VV_Viviwax, WaxTarget) - wax), stock.CountOf(VVThingDefOf.VV_Vivicream) / CreamReserveForWax);
            }
            else if (recipe == VVRecipeDefOf.Make_VV_BasicWorkerCostume)
            {
                batches = UnityEngine.Mathf.Min(MembersNeedingCostume() - stock.CountOf(VVThingDefOf.VV_BasicWorkerCostume), stock.CountOf(ThingDefOf.Cloth) / ClothForCostume);
            }
            else if (recipe == VVRecipeDefOf.Make_VV_Slingshot)
            {
                var materials = UnityEngine.Mathf.Min(stock.CountOf(ThingDefOf.WoodLog) / 20, stock.CountOf(VVThingDefOf.VV_Viviwax) / 10);
                batches = UnityEngine.Mathf.Min(SlingersNeedingWeapon() - stock.CountOf(VVThingDefOf.VV_Slingshot), materials);
            }
            else if (recipe == VVRecipeDefOf.VV_FilteringHoney)
            {
                batches = stock.CountOf(VVThingDefOf.VV_RawHoney) / 20;
            }
            else if (recipe == VVRecipeDefOf.VV_Make_HoneyPollenCake)
            {
                var materials = UnityEngine.Mathf.Min(stock.CountOf(VVThingDefOf.VV_FilteredHoney) / 3, stock.CountOf(VVThingDefOf.VV_Pollen) / 6);
                batches = UnityEngine.Mathf.Min(Batches(recipe, CakeTarget - stock.CountOf(VVThingDefOf.VV_HoneyPollenCake)), materials);
            }
            return UnityEngine.Mathf.Max(0, batches);
        }

        // 모자란 양을 채울 묶음 수(한 번에 나오는 양으로 나눠 올림)
        private static int Batches(RecipeDef recipe, int shortfall)
        {
            if (shortfall <= 0) { return 0; }

            var perBatch = UnityEngine.Mathf.Max(1, recipe.products.NullOrEmpty() ? 1 : recipe.products[0].count);
            return (shortfall + perBatch - 1) / perBatch;
        }

        // 그 지시서로 지금 일하는 구성원(바닐라 지시서 잡)
        private Pawn WorkerOn(Bill bill, Pawn except)
        {
            var pawns = _owner.lord?.ownedPawns;
            if (pawns == null) { return null; }

            for (int i = 0; i < pawns.Count; ++i)
            {
                if (pawns[i] != except && pawns[i].CurJob?.bill == bill) { return pawns[i]; }
            }
            return null;
        }

        // 만들고 있는 지시서: 그 지시서로 일하는 구성원이 있거나 만들다 만 것이 걸려 있다.
        private bool InProgress(Bill bill, Pawn except = null)
        {
            if (bill is Bill_ProductionWithUft uftBill && uftBill.BoundWorker != null && uftBill.BoundWorker != except) { return true; }
            return WorkerOn(bill, except) != null;
        }

        // 그 지시를 지금 만들고 있는 묶음 수(한 자리에서는 한 마리만 일하므로 자리마다 하나까지)
        public int BatchesInProgress(RecipeDef recipe, Pawn except = null)
        {
            var count = 0;
            for (int i = 0; i < _workSpots.Count; ++i)
            {
                var bill = FindBill(_workSpots[i], recipe);
                if (bill != null && InProgress(bill, except)) { count++; }
            }
            return count;
        }

        // 이 비비가 그 지시서를 새로 시작해도 되는지: 이어 만드는 자기 것이거나, 지금 만드는 묶음이 아직 만들 묶음 수보다 적을 때.
        // 생산 주기 사이에 다른 자리에서 묶음이 끝나 목표를 채웠으면, 켜 둔 지시서가 다음 주기에 멈추기 전에도 바로 막는다.
        public bool CanStartBill(Pawn pawn, Bill bill)
        {
            if (bill == null) { return true; }
            if (bill is Bill_ProductionWithUft uftBill && uftBill.BoundWorker == pawn) { return true; }

            return BatchesInProgress(bill.recipe, pawn) < WantedBatches(bill.recipe);
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

        // 채집 잡 기버의 우선도: 채집 자원(꿀·꽃가루·프로폴리스) 중 가장 모자란 것의 모자란 정도
        public float GatherPriority()
        {
            return UnityEngine.Mathf.Max(Need(VVRecipeDefOf.VV_GatherHoney), UnityEngine.Mathf.Max(Need(VVRecipeDefOf.VV_GatherPollen), Need(VVRecipeDefOf.VV_GatherPropolis)));
        }

        // 만들다 만 것은 시작한 비비만 이어서 만들 수 있으므로(바닐라), 그 비비에게는 가장 먼저 할 일이고 다른 비비에게는 없는 일이다.
        public float CraftPriority(Pawn pawn)
        {
            var best = 0f;
            for (int i = 0; i < _workSpots.Count; ++i)
            {
                var spot = _workSpots[i];
                if (!spot.Spawned) { continue; }

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
        // 마나가 없어 채우지 않는 새싹은 뺀다.
        // 마법식물의 마나 소모 규칙 값은 환경 마나 흡수와 스스로 내는 마나를 빼지 않아 실제로 주는 비료보다 몇 배 크고 시각·기온에 따라 출렁이므로,
        // 둥지 비비가 실제로 준 양을 잰다.
        public float FertilizerPerDay()
        {
            var perDay = _recentPlantFertilizer / FertilizerReserveDays;
            var refuelables = Map.listerThings.ThingsInGroup(ThingRequestGroup.Refuelable);
            for (int i = 0; i < refuelables.Count; ++i)
            {
                if (refuelables[i].Faction != _owner.Faction || JobGiver_ViviNestRefuel.IsStarvedSeedling(refuelables[i])) { continue; }

                // 쓸 때만 태우는 것(대형 화분 등)은 하루 소모량을 알 수 없어 뺀다.
                var props = refuelables[i].TryGetComp<CompRefuelable>()?.Props;
                if (props == null || props.consumeFuelOnlyWhenUsed || props.externalTicking || !props.fuelFilter.Allows(VVThingDefOf.VV_Fertilizer)) { continue; }

                perDay += props.fuelConsumptionRate / props.FuelMultiplierCurrentDifficulty;
            }
            return perDay;
        }

        // 원목 비축 목표(나무 베기 잡 기버와 보유량 균형이 함께 쓴다)
        public int WoodStockTarget()
        {
            return Target(ThingDefOf.WoodLog, WoodTarget + UnityEngine.Mathf.CeilToInt(WoodPerDay() * WoodReserveDays));
        }

        // 둥지가 하루에 태우는 원목: 원목을 연료로 쓰는 둥지 것(냉각기) 중 지금 연료를 채우는 것의 바닐라 연료 소모율.
        public float WoodPerDay()
        {
            var perDay = 0f;
            var refuelables = Map.listerThings.ThingsInGroup(ThingRequestGroup.Refuelable);
            for (int i = 0; i < refuelables.Count; ++i)
            {
                var thing = refuelables[i];
                if (thing.Faction != _owner.Faction || JobGiver_ViviNestRefuel.IsIdleCooler(_owner, thing)) { continue; }

                var props = thing.TryGetComp<CompRefuelable>()?.Props;
                if (props == null || props.consumeFuelOnlyWhenUsed || props.externalTicking || !props.fuelFilter.Allows(ThingDefOf.WoodLog)) { continue; }

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

        public int MembersNeedingCostume()
        {
            var pawns = _owner.lord?.ownedPawns;
            var count = 0;
            if (pawns != null)
            {
                for (int i = 0; i < pawns.Count; ++i)
                {
                    if (NeedsCostume(pawns[i])) { count++; }
                }
            }
            return count;
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

        public int SlingersNeedingWeapon()
        {
            var pawns = _owner.lord?.ownedPawns;
            var count = 0;
            if (pawns != null)
            {
                for (int i = 0; i < pawns.Count; ++i)
                {
                    if (NeedsSlingshot(pawns[i])) { count++; }
                }
            }
            return count;
        }

        public static bool NeedsSlingshot(Pawn pawn)
        {
            return pawn.kindDef == VVPawnKindDefOf.VV_WanderingViviSlinger && pawn.DevelopmentalStage.Adult()
                && pawn.equipment != null && pawn.equipment.Primary == null && !pawn.WorkTagIsDisabled(WorkTags.Violent);
        }

        public void ExposeData()
        {
            Scribe_Collections.Look(ref _workSpots, "workSpots", LookMode.Reference);
            Scribe_Values.Look(ref _recentPlantFertilizer, "recentPlantFertilizer");

            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                _workSpots.RemoveAll(s => s == null);
            }
        }
    }
}
