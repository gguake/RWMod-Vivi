using RimWorld;
using System.Collections.Generic;
using Verse;
using Verse.AI;

namespace VVRace
{
    public class ViviNestProduction : IExposable
    {
        public const int RefreshInterval = 600;

        public const int HoneyCapacity = 600;
        public const float WinterTemperature = 0f;
        public const int PollenTarget = 60;
        public const int PropolisTarget = 20;
        public const int CreamTarget = 60;
        public const int WaxTarget = 80;
        public const int WoodTarget = 60;
        public const float WoodReserveDays = FertilizerReserveDays;
        public const int MinFertilizerTarget = 120;
        public const int MaxFertilizerTarget = 300;
        public const float FertilizerReserveDays = 5f;
        public const int CakeTarget = 40;
        public const int MedicineTarget = 10;
        public const int CreamReserveForWax = 30;
        public const float MinFoodDaysForCream = 2f;
        public const float ProcessingWeight = 0.3f;
        public const int WorkersPerWorkSpot = 2;
        public const float GatherRadius = Building_GatherWorkTable.GatherMaxRadius;
        public const float GatherYieldFactor = 0.5f;
        private static readonly float RecentPlantFertilizerDecay = UnityEngine.Mathf.Exp(-RefreshInterval / (FertilizerReserveDays * GenDate.TicksPerDay));

        private readonly LordJob_ViviNest _owner;

        private List<Building_WorkTable> _workSpots = new List<Building_WorkTable>();

        private static List<RecipeDef> _workRecipes;
        private static readonly List<RecipeDef> _tmpRecipes = new List<RecipeDef>();
        private static readonly List<int> _tmpSpotLoad = new List<int>();
        private static readonly List<int> _tmpChosen = new List<int>();

        private Dictionary<RecipeDef, float> _billNeed = new Dictionary<RecipeDef, float>();
        private float _woodNeed;
        private float _fertilizerNeed;
        private float _medicineNeed;
        private int _fertilizerTarget = MinFertilizerTarget;
        private int _woodStockTarget = -1;
        private float _recentPlantFertilizer;
        private int _winterDays = -1;

        private readonly HashSet<IntVec3> _spotInteractionCells = new HashSet<IntVec3>();
        private bool _spotInteractionCellsDirty = true;

        public const int HealrootRetryMaxTicks = 1250;
        public const int TreeRetryMaxTicks = 1250;
        public const int GatherRetryMaxTicks = 600;
        private int _healrootRetryTick = -1;
        private int _treeRetryTick = -1;
        private readonly Dictionary<RecipeDef, int> _gatherRetryTicks = new Dictionary<RecipeDef, int>();

        public const int GatherStatsCacheTicks = 2500;
        private readonly Dictionary<(Pawn, RecipeDef), (int tick, bool ok)> _gatherStats = new Dictionary<(Pawn, RecipeDef), (int tick, bool ok)>();
        private static readonly List<(Pawn, RecipeDef)> _tmpStaleGatherStats = new List<(Pawn, RecipeDef)>();

        public ViviNestProduction(LordJob_ViviNest owner)
        {
            _owner = owner;
        }

        private Map Map => _owner.Map;
        private ViviNestStock Stock => _owner.Stock;

        public IReadOnlyList<Building_WorkTable> WorkSpots => _workSpots;

        private static List<RecipeDef> WorkRecipes
        {
            get
            {
                if (_workRecipes == null)
                {
                    _workRecipes = new List<RecipeDef>
                    {
                        VVRecipeDefOf.Make_VV_BasicWorkerCostume,
                        VVRecipeDefOf.Make_VV_Childrenswear,
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

        public int CachedWoodStockTarget => _woodStockTarget >= 0 ? _woodStockTarget : WoodStockTarget();

        public bool HealrootWaiting => Find.TickManager.TicksGame < _healrootRetryTick;
        public bool TreeWaiting => Find.TickManager.TicksGame < _treeRetryTick;
        public bool GatherWaiting(RecipeDef recipe) => _gatherRetryTicks.TryGetValue(recipe, out var tick) && Find.TickManager.TicksGame < tick;

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
            var workers = CountMembers(p =>
            {
                var stage = p.DevelopmentalStage;
                return !stage.Baby() && !stage.Newborn();
            });
            return UnityEngine.Mathf.Max(1, (workers + WorkersPerWorkSpot - 1) / WorkersPerWorkSpot);
        }

        private int CountMembers(System.Predicate<Pawn> match) => _owner.lord?.ownedPawns.Count(match) ?? 0;

        private bool IsWorkSpotInUse(Building_WorkTable spot)
        {
            if (Map.reservationManager.IsReservedByAnyoneOf(spot, _owner.Faction)) { return true; }
            foreach (var bill in spot.BillStack)
            {
                if (bill is Bill_ProductionWithUft uftBill && uftBill.BoundUft != null) { return true; }
            }
            return false;
        }

        private bool TryPlaceWorkSpot()
        {
            var def = VVThingDefOf.VV_ViviNestWorkSpot;
            var map = Map;
            var fallback = IntVec3.Invalid;
            foreach (var room in _owner.Territory.Rooms)
            {
                foreach (var cell in room.Cells)
                {
                    var interaction = ThingUtility.InteractionCellWhenAt(def, cell, Rot4.South, map);
                    if (!CanPlaceSpotCell(cell) || !CanPlaceSpotCell(interaction)) { continue; }
                    if (IsWorkSpotInteractionCell(cell) || IsWorkSpotInteractionCell(interaction)) { continue; }

                    if (!BeekeepingUtility.TouchesWall(cell, map) && !BeekeepingUtility.TouchesWall(interaction, map))
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

        private void PlaceWorkSpot(IntVec3 cell)
        {
            var spot = (Building_WorkTable)ThingMaker.MakeThing(VVThingDefOf.VV_ViviNestWorkSpot);
            spot.SetFaction(_owner.Faction);
            GenSpawn.Spawn(spot, cell, Map, Rot4.South);

            foreach (var recipe in WorkRecipes)
            {
                if (recipe == null) { continue; }

                var bill = recipe.MakeNewBill();
                if (bill is Bill_Production production)
                {
                    production.repeatMode = BillRepeatModeDefOf.Forever;
                    production.SetStoreMode(BillStoreModeDefOf.DropOnFloor);
                }
                spot.BillStack.AddBill(bill);
            }
            _workSpots.Add(spot);
            _spotInteractionCellsDirty = true;
        }

        private bool CanPlaceSpotCell(IntVec3 c)
        {
            var map = Map;
            if (!c.InBounds(map) || !c.Standable(map)) { return false; }
            if (!_owner.Territory.IsRoomCell(c) || !_owner.Territory.IsClearSpot(c)) { return false; }
            foreach (var thing in c.GetThingList(map))
            {
                if (thing is Pawn || thing.def.category == ThingCategory.Item) { return false; }
            }
            return true;
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

            var honeyTarget = HoneyTarget();
            var honey = rawHoney + filteredHoney;
            _billNeed[VVRecipeDefOf.VV_GatherHoney] = honey >= honeyTarget ? 0f : (FoodReserveDays() < MinFoodDaysForCream ? 1f : Shortage(honey, honeyTarget));
            _billNeed[VVRecipeDefOf.VV_GatherPollen] = Shortage(pollen, PollenTarget);
            _billNeed[VVRecipeDefOf.VV_GatherPropolis] = Shortage(propolis, PropolisTarget);
            _billNeed[VVRecipeDefOf.Make_VV_BasicWorkerCostume] = 1f;
            _billNeed[VVRecipeDefOf.Make_VV_Childrenswear] = 1f;
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
                _workSpots[i]?.BillStack.Bills.InsertionSort((a, b) => NeedOf(b).CompareTo(NeedOf(a)));
            }
        }

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
                        bill.suspended = !_tmpChosen.Contains(i) && !(bill is Bill_ProductionWithUft uftBill && uftBill.BoundWorker != null);
                    }
                }
            }
        }

        public float Need(RecipeDef recipe) => _billNeed.TryGetValue(recipe, out var need) ? need : 0f;

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
            else if (recipe == VVRecipeDefOf.Make_VV_BasicWorkerCostume || recipe == VVRecipeDefOf.Make_VV_Childrenswear)
            {
                var costume = recipe.ProducedThingDef;
                batches = UnityEngine.Mathf.Min(MembersNeedingCostume(costume) - stock.CountOf(costume), stock.BatchesFor(recipe));
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

        private static int Batches(RecipeDef recipe, int shortfall)
        {
            if (shortfall <= 0) { return 0; }

            var perBatch = UnityEngine.Mathf.Max(1, recipe.products.NullOrEmpty() ? 1 : recipe.products[0].count);
            return (shortfall + perBatch - 1) / perBatch;
        }

        private bool InProgress(Bill bill, Pawn except = null)
        {
            if (bill is Bill_ProductionWithUft uftBill && uftBill.BoundWorker != null && uftBill.BoundWorker != except) { return true; }

            var pawns = _owner.lord?.ownedPawns;
            if (pawns == null) { return false; }

            for (int i = 0; i < pawns.Count; ++i)
            {
                if (pawns[i] != except && pawns[i].CurJob?.bill == bill) { return true; }
            }
            return false;
        }

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

        public bool CanStartBill(Pawn pawn, Bill bill)
        {
            if (bill == null) { return true; }
            if (bill is Bill_ProductionWithUft uftBill && uftBill.BoundWorker == pawn) { return true; }

            return BatchesInProgress(bill.recipe, pawn) < WantedBatches(bill.recipe);
        }

        private static float Shortage(int have, int target) => target > 0 ? UnityEngine.Mathf.Clamp01(1f - (float)have / target) : 0f;

        public float NeedOf(Bill bill)
        {
            return bill != null && !bill.suspended && _billNeed.TryGetValue(bill.recipe, out var need) ? need : 0f;
        }

        public float GatherPriority()
        {
            return UnityEngine.Mathf.Max(Need(VVRecipeDefOf.VV_GatherHoney), UnityEngine.Mathf.Max(Need(VVRecipeDefOf.VV_GatherPollen), Need(VVRecipeDefOf.VV_GatherPropolis)));
        }

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

        public int Target(ThingDef def, int baseTarget) => baseTarget + _owner.Planner.ResourceNeeded(def);

        private void UpdateFertilizerTarget()
        {
            _recentPlantFertilizer *= RecentPlantFertilizerDecay;
            var target = UnityEngine.Mathf.CeilToInt(FertilizerPerDay() * FertilizerReserveDays);
            _fertilizerTarget = Target(VVThingDefOf.VV_Fertilizer, UnityEngine.Mathf.Clamp(target, MinFertilizerTarget, MaxFertilizerTarget));
        }

        public float FertilizerPerDay()
        {
            return FuelPerDay(VVThingDefOf.VV_Fertilizer, (nest, t) => JobGiver_ViviNestRefuel.IsStarvedSeedling(t), _recentPlantFertilizer / FertilizerReserveDays);
        }

        public int WoodStockTarget()
        {
            return Target(ThingDefOf.WoodLog, WoodTarget + UnityEngine.Mathf.CeilToInt(WoodPerDay() * WoodReserveDays));
        }

        public float WoodPerDay()
        {
            return FuelPerDay(ThingDefOf.WoodLog, (nest, t) => JobGiver_ViviNestRefuel.IsIdleCooler(nest, t), 0f);
        }

        private float FuelPerDay(ThingDef fuel, System.Func<LordJob_ViviNest, Thing, bool> skip, float perDay)
        {
            var refuelables = Map.listerThings.ThingsInGroup(ThingRequestGroup.Refuelable);
            for (int i = 0; i < refuelables.Count; ++i)
            {
                var thing = refuelables[i];
                if (thing.Faction != _owner.Faction || skip(_owner, thing)) { continue; }

                var props = thing.TryGetComp<CompRefuelable>()?.Props;
                if (props == null || props.consumeFuelOnlyWhenUsed || props.externalTicking || !props.fuelFilter.Allows(fuel)) { continue; }

                perDay += props.fuelConsumptionRate / props.FuelMultiplierCurrentDifficulty;
            }
            return perDay;
        }

        public void Notify_PlantFertilized(int count)
        {
            _recentPlantFertilizer += count;
        }

        public float FoodReserveDays()
        {
            var perDay = FoodPerDay();
            return perDay > 0f ? Stock.FoodNutrition / perDay : float.MaxValue;
        }

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

        public int HoneyTarget()
        {
            var winterDays = WinterDays();
            if (winterDays <= 0) { return HoneyCapacity; }

            var perUnit = VVThingDefOf.VV_FilteredHoney.GetStatValueAbstract(StatDefOf.Nutrition);
            return perUnit > 0f ? UnityEngine.Mathf.Max(HoneyCapacity, UnityEngine.Mathf.CeilToInt(FoodPerDay() * winterDays / perUnit)) : HoneyCapacity;
        }

        public int MembersNeedingCostume(ThingDef costume) => CountMembers(p => NeedsCostume(p) && CostumeFor(p) == costume);

        public static ThingDef CostumeFor(Pawn pawn)
        {
            if (pawn.apparel == null || pawn.IsRoyalVivi()) { return null; }

            var stage = pawn.DevelopmentalStage;
            if (stage.Adult()) { return VVThingDefOf.VV_BasicWorkerCostume; }
            if (stage.Child()) { return VVThingDefOf.VV_Childrenswear; }
            return null;
        }

        public static bool NeedsCostume(Pawn pawn)
        {
            var costume = CostumeFor(pawn);
            if (costume == null) { return false; }

            foreach (var apparel in pawn.apparel.WornApparel)
            {
                if (!ApparelUtility.CanWearTogether(apparel.def, costume, pawn.RaceProps.body) && apparel.HitPoints >= apparel.MaxHitPoints * 0.4f)
                {
                    return false;
                }
            }
            return true;
        }

        public int SlingersNeedingWeapon() => CountMembers(p => NeedsSlingshot(p));

        public static bool NeedsSlingshot(Pawn pawn)
        {
            return pawn.kindDef == VVPawnKindDefOf.VV_WanderingViviSlinger && pawn.DevelopmentalStage.Adult()
                && pawn.equipment != null && pawn.equipment.Primary == null && !pawn.WorkTagIsDisabled(WorkTags.Violent);
        }

        public void ExposeData()
        {
            Scribe_Collections.Look(ref _workSpots, "workSpots", LookMode.Reference);
            Scribe_Values.Look(ref _recentPlantFertilizer, "recentPlantFertilizer");
            Scribe_Collections.Look(ref _billNeed, "billNeed", LookMode.Def, LookMode.Value);
            Scribe_Values.Look(ref _woodNeed, "woodNeed");
            Scribe_Values.Look(ref _fertilizerNeed, "fertilizerNeed");
            Scribe_Values.Look(ref _medicineNeed, "medicineNeed");
            Scribe_Values.Look(ref _fertilizerTarget, "fertilizerTarget", MinFertilizerTarget);
            Scribe_Values.Look(ref _woodStockTarget, "woodStockTarget", -1);

            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                _workSpots.RemoveAll(s => s == null);
            }
        }
    }
}
