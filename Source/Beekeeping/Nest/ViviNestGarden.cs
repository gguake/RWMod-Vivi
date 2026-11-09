using RimWorld;
using System.Collections.Generic;
using Verse;

namespace VVRace
{
    // 둥지 정원: 채집할 꽃이 모자라면 둥지 채집 범위에 꽃을 심고, 옷감이 모자라면 바깥 보관 칸 옆 작은 목화밭을 가꾼다.
    // 심기·수확은 바닐라 재배 잡을 그대로 쓴다(구역 없이).
    public class ViviNestGarden : IExposable
    {
        // 꿀 꽃 목표: 40송이가 다섯 마리 본전이므로(자동 테스트) 구성원당 8송이(최소 40), 겨울이 있는 땅에서는 겨울 비축을 위해 1.5배.
        public const int MinFlowerTarget = 40;
        public const int FlowersPerMember = 8;
        public const float WinterFlowerFactor = 1.5f;
        public const int CottonCellCount = 6;
        public const int ClothTarget = 100;
        private const int FlowerCountInterval = 2500;
        private const float CottonSearchRadius = 12f;

        private readonly LordJob_ViviNest _owner;

        private List<IntVec3> _cottonCells = new List<IntVec3>();

        private int _flowerCount;
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

        // 둥지 채집 범위 안의 꿀 꽃(덜 자란 것 포함, 심어 둔 것을 다시 심지 않게)
        public int FlowerCount
        {
            get
            {
                RefreshFlowerCounts();
                return _flowerCount;
            }
        }

        // 그중 채집할 만큼 자란 꽃(재채집 대기 중인 것 포함). 겨울에 갓 심어 자라지 못하는 꽃은 먹이가 되지 않는다.
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

        public void Notify_FlowerSown() => _flowerCount++;

        public int FlowerTarget()
        {
            var members = _owner.lord?.ownedPawns.Count ?? 0;
            var target = UnityEngine.Mathf.Max(MinFlowerTarget, FlowersPerMember * members);
            return _owner.Production.WinterDays() > 0 ? UnityEngine.Mathf.CeilToInt(target * WinterFlowerFactor) : target;
        }

        public bool FlowerSpotSearchPaused => Find.TickManager.TicksGame < _noFlowerSpotUntil;

        public void Notify_NoFlowerSpot() => _noFlowerSpotUntil = Find.TickManager.TicksGame + FlowerCountInterval;

        private void CountFlowers()
        {
            _flowerCount = 0;
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

                _flowerCount++;
                if (plant.CanGatherable(VVStatDefOf.VV_PlantHoneyGatherYield)) { _gatherableFlowerCount++; }
            }
        }

        // 꿀 꽃 판정을 정의별로 기억한다(추상 스탯 계산이 1~2 µs라 꽃 세기·씨 뿌리기 칸마다 되풀이됐다). 추상 스탯은 정의와 시나리오 스탯 배율
        // (바닐라 StatWorker.FinalizeValue의 Find.Scenario.GetStatFactor)로만 정해지고 시나리오는 게임 안에서 바뀌지 않으므로, 게임이 바뀌면 비운다.
        // 게임은 약참조로 기억해 끝난 게임 객체를 붙잡지 않는다.
        private static readonly Dictionary<ThingDef, bool> _honeyFlowerDefs = new Dictionary<ThingDef, bool>();
        private static readonly System.WeakReference<Game> _honeyFlowerDefsGame = new System.WeakReference<Game>(null);

        public static bool IsHoneyFlowerDef(ThingDef def)
        {
            if (def == null || def.plant == null || def.plant.IsTree) { return false; }

            var game = Current.Game;
            if (!_honeyFlowerDefsGame.TryGetTarget(out var memoGame) || memoGame != game)
            {
                _honeyFlowerDefs.Clear();
                _honeyFlowerDefsGame.SetTarget(game);
            }

            if (!_honeyFlowerDefs.TryGetValue(def, out var honeyFlower))
            {
                honeyFlower = def.GetStatValueAbstract(VVStatDefOf.VV_PlantHoneyGatherYield) > 0f;
                _honeyFlowerDefs[def] = honeyFlower;
            }
            return honeyFlower;
        }

        // 목화밭은 바깥 보관 칸 둘레의 비옥한 빈 땅에 한 번 정해 둔다(자리를 잃으면 다시 고른다).
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

        // 이미 정한 밭은 사람·수확물처럼 잠깐 있는 것이 아니라 건물·구역·지붕처럼 오래가는 것만 보고 버린다.
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

        // 둥지 씨 뿌리기(JobGiver_ViviNestGarden.SowJob)가 심을 수 있는 칸: 꿀 꽃이 자라는 칸은 베지 않고, 둘레에 나무처럼 옆 칸 심기를 막는 식물이 있으면
        // 바닐라 씨 뿌리기 판정(PlantUtility.AdjacentSowBlocker)이 막는다. 그런 칸을 밭으로 두면 끝내 심지 못하므로 고르지 않고, 생기면 다른 칸으로 옮긴다.
        // 이미 목화가 자라는 칸은 거둘 때까지 둔다. (자동 테스트 p3 B6: 습지 맵에서 버드나무 옆 밭 3칸이 며칠째 비어 있었다)
        // 먼저 목화가 그 칸에 자랄 수 있어야 한다(바닐라 CanEverPlantAt, 나무 아닌 식물은 베고 심는다, 지금 기온은 보지 않는다).
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
