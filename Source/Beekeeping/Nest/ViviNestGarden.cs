using RimWorld;
using System.Collections.Generic;
using Verse;

namespace VVRace
{
    public class ViviNestGarden : IExposable
    {
        public const int MinFlowerTarget = 40;
        public const int FlowersPerMember = 8;
        public const float WinterFlowerFactor = 1.5f;
        public const float SteadyFlowerFraction = 0.3f;
        public const int CottonCellCount = 6;
        public const int ClothTarget = 100;
        private const int FlowerCountInterval = 2500;
        private const float CottonSearchRadius = 12f;

        private readonly LordJob_ViviNest _owner;

        private List<IntVec3> _cottonCells = new List<IntVec3>();

        private int _flowerCount;
        private int _steadyFlowerCount;
        private int _gatherableFlowerCount;
        private int _flowerCountTick = -99999;
        private int _noFlowerSpotUntil = -99999;

        public ViviNestGarden(LordJob_ViviNest owner)
        {
            _owner = owner;
        }

        private Map Map => _owner.Map;

        public IReadOnlyList<IntVec3> CottonCells => _cottonCells;

        public bool IsCottonCell(IntVec3 cell) => _cottonCells.Contains(cell);

        public int FlowerCount
        {
            get
            {
                RefreshFlowerCounts();
                return _flowerCount;
            }
        }

        // 자라기만 하면 언제든 꿀을 딸 수 있는 꽃의 수. 개화기에만 딸 수 있는 꽃은 빠진다.
        public int SteadyFlowerCount
        {
            get
            {
                RefreshFlowerCounts();
                return _steadyFlowerCount;
            }
        }

        public int GatherableFlowerCount
        {
            get
            {
                RefreshFlowerCounts();
                return _gatherableFlowerCount;
            }
        }

        private void RefreshFlowerCounts()
        {
            var ticks = Find.TickManager.TicksGame;
            if (ticks - _flowerCountTick >= FlowerCountInterval)
            {
                _flowerCountTick = ticks;
                CountFlowers();
            }
        }

        public void Notify_FlowerSown()
        {
            _flowerCount++;
            _steadyFlowerCount++;
        }

        public int FlowerTarget()
        {
            var members = _owner.lord?.ownedPawns.Count ?? 0;
            var target = UnityEngine.Mathf.Max(MinFlowerTarget, FlowersPerMember * members);
            return _owner.Production.WinterDays() > 0 ? UnityEngine.Mathf.CeilToInt(target * WinterFlowerFactor) : target;
        }

        // 개화기 꽃만으로 목표를 채웠다가 개화가 끝나면 한꺼번에 꿀이 끊기므로, 언제든 딸 수 있는 꽃을 이만큼은 따로 유지한다.
        public int SteadyFlowerFloor() => UnityEngine.Mathf.Max(LordJob_ViviNest.MinFlowersToStay, UnityEngine.Mathf.CeilToInt(FlowerTarget() * SteadyFlowerFraction));

        public bool NeedsMoreFlowers => FlowerCount < FlowerTarget() || SteadyFlowerCount < SteadyFlowerFloor();

        public bool FlowerSpotSearchPaused => Find.TickManager.TicksGame < _noFlowerSpotUntil;

        public void Notify_NoFlowerSpot() => _noFlowerSpotUntil = Find.TickManager.TicksGame + FlowerCountInterval;

        private void CountFlowers()
        {
            _flowerCount = 0;
            _steadyFlowerCount = 0;
            _gatherableFlowerCount = 0;

            var map = Map;
            var territory = _owner.Territory;
            var cells = territory.ActivityCells;
            var cellCount = territory.ActivityCellCount(ViviNestProduction.GatherRadius);
            for (int i = 0; i < cellCount; ++i)
            {
                var cell = cells[i];
                var plant = cell.GetPlant(map);
                if (plant == null || !IsHoneyFlowerDef(plant.def)) { continue; }

                var minGrowth = AbstractMinGrowth(plant.def);
                var gatherable = plant.CanGatherable(VVStatDefOf.VV_PlantHoneyGatherYield);
                if (gatherable) { _gatherableFlowerCount++; }

                if (gatherable || StillGrowing(plant, minGrowth))
                {
                    _flowerCount++;
                    if (minGrowth <= 1f) { _steadyFlowerCount++; }
                }
            }
        }

        // 꿀 꽃 정의인지만 본다. 건설·파종에서 건드리지 않을 꽃을 가릴 때 쓴다(지금 꿀을 딸 수 있는지는 IsHoneySource).
        public static bool IsHoneyFlowerDef(ThingDef def)
        {
            if (def == null || def.plant == null || def.plant.IsTree) { return false; }

            return GameComponent_Mana.HasHoneyYield(def);
        }

        // 지금 꿀을 딸 수 있거나 자라기만 하면 딸 수 있게 되는 식물인지. 꿀 수율이 있는 정의라도 조건이 맞을 때만
        // 채집할 수 있는 식물(개화기에만 피는 꽃 등)은 그 조건이 맞는 동안에만 센다. 꿀 수율이 있는 정의인지는 호출하는 쪽에서 확인한다.
        public static bool IsHoneySource(Plant plant)
        {
            return StillGrowing(plant, AbstractMinGrowth(plant.def)) || plant.CanGatherable(VVStatDefOf.VV_PlantHoneyGatherYield);
        }

        // 정의의 채집 최소 성장도가 1 이하면 자라는 것만으로 채집할 수 있게 된다. 1을 넘으면 성장과 무관한 조건이 걸린 식물이라
        // 어릴 때는 세지 않고, 다 자란 뒤 실제 채집 가능 여부로만 판정한다.
        private static bool StillGrowing(Plant plant, float minGrowth) => minGrowth <= 1f && plant.Growth < minGrowth;

        // 정의별 채집 최소 성장도(추상 값) 메모. 추상 스탯은 시나리오 스탯 배율에 따라 달라질 수 있어 게임이 바뀌면 비운다.
        private static readonly Dictionary<ThingDef, float> _abstractMinGrowth = new Dictionary<ThingDef, float>();
        private static System.WeakReference<Game> _abstractMinGrowthGame;

        private static float AbstractMinGrowth(ThingDef def)
        {
            var game = Current.Game;
            if (_abstractMinGrowthGame == null || !_abstractMinGrowthGame.TryGetTarget(out var cachedGame) || cachedGame != game)
            {
                _abstractMinGrowth.Clear();
                _abstractMinGrowthGame = game != null ? new System.WeakReference<Game>(game) : null;
            }

            if (!_abstractMinGrowth.TryGetValue(def, out var value))
            {
                value = def.GetStatValueAbstract(VVStatDefOf.VV_MinGrowthPlantGatherable);
                _abstractMinGrowth[def] = value;
            }
            return value;
        }

        public void EnsureCottonCells()
        {
            var map = Map;
            _cottonCells.RemoveAll(c => !StillUsable(c));
            if (_cottonCells.Count >= CottonCellCount) { return; }

            var storage = _owner.Territory.OutdoorStorageCells;
            var origin = storage.Count > 0 ? storage[storage.Count - 1] : _owner.NestCell;
            var cellCount = GenRadial.NumCellsInRadius(CottonSearchRadius);
            for (int i = 0; i < cellCount && _cottonCells.Count < CottonCellCount; ++i)
            {
                var cell = origin + GenRadial.RadialPattern[i];
                if (_cottonCells.Contains(cell) || !cell.InBounds(map)) { continue; }
                if (!_owner.Planner.IsFreeOutdoorCell(cell, allowPlants: true, ignoreCotton: true)) { continue; }
                if (!IsSowable(cell)) { continue; }

                _cottonCells.Add(cell);
            }
        }

        private bool StillUsable(IntVec3 cell)
        {
            var map = Map;
            if (!cell.InBounds(map) || cell.Roofed(map) || cell.GetEdifice(map) != null || map.zoneManager.ZoneAt(cell) != null) { return false; }
            if (_owner.Territory.IsTerritoryCell(cell)) { return false; }

            foreach (var thing in cell.GetThingList(map))
            {
                if (BeekeepingUtility.IsStructure(thing)) { return false; }
            }

            return IsSowable(cell);
        }

        private bool IsSowable(IntVec3 cell)
        {
            var map = Map;
            if (!VVThingDefOf.Plant_Cotton.CanEverPlantAt(cell, map, out _, canWipePlantsExceptTree: true, checkMapTemperature: false).Accepted) { return false; }

            var plant = cell.GetPlant(map);
            if (plant != null && plant.def == VVThingDefOf.Plant_Cotton) { return true; }
            if (plant != null && IsHoneyFlowerDef(plant.def)) { return false; }
            return PlantUtility.AdjacentSowBlocker(VVThingDefOf.Plant_Cotton, cell, map) == null;
        }

        public void ExposeData()
        {
            Scribe_Collections.Look(ref _cottonCells, "cottonCells", LookMode.Value);
        }
    }
}
