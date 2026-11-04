using RimWorld;
using System.Collections.Generic;
using Verse;

namespace VVRace
{
    // 둥지 정원: 채집할 꽃이 모자라면 채집 장소 둘레에 꽃을 심고, 옷감이 모자라면 바깥 보관 칸 옆 작은 목화밭을 가꾼다.
    // 심기·수확은 바닐라 재배 잡을 그대로 쓴다(구역 없이).
    public class ViviNestGarden : IExposable
    {
        public const int FlowerTarget = 40;
        public const int CottonCellCount = 6;
        public const int ClothTarget = 100;
        private const int FlowerCountInterval = 2500;
        private const float CottonSearchRadius = 12f;

        private readonly LordJob_ViviNest _owner;

        private List<IntVec3> _cottonCells = new List<IntVec3>();

        private int _flowerCount;
        private int _flowerCountTick = -99999;
        private int _noFlowerSpotUntil = -99999;

        public ViviNestGarden(LordJob_ViviNest owner)
        {
            _owner = owner;
        }

        private Map Map => _owner.Map;

        public IReadOnlyList<IntVec3> CottonCells => _cottonCells;

        public bool IsCottonCell(IntVec3 cell) => _cottonCells.Contains(cell);

        // 채집 반경 안의 꿀 꽃(덜 자란 것 포함, 심어 둔 것을 다시 심지 않게)
        public int FlowerCount
        {
            get
            {
                var ticks = Find.TickManager.TicksGame;
                if (ticks - _flowerCountTick >= FlowerCountInterval)
                {
                    _flowerCountTick = ticks;
                    _flowerCount = CountFlowers();
                }
                return _flowerCount;
            }
        }

        public void Notify_FlowerSown() => _flowerCount++;

        public bool FlowerSpotSearchPaused => Find.TickManager.TicksGame < _noFlowerSpotUntil;

        public void Notify_NoFlowerSpot() => _noFlowerSpotUntil = Find.TickManager.TicksGame + FlowerCountInterval;

        private int CountFlowers()
        {
            var spot = _owner.Production.GatherSpot;
            if (spot == null) { return 0; }

            var map = Map;
            var count = 0;
            var cellCount = GenRadial.NumCellsInRadius(Building_GatherWorkTable.GatherMaxRadius);
            for (int i = 0; i < cellCount; ++i)
            {
                var cell = spot.Position + GenRadial.RadialPattern[i];
                if (cell.InBounds(map) && IsHoneyFlowerDef(cell.GetPlant(map)?.def))
                {
                    count++;
                }
            }
            return count;
        }

        public static bool IsHoneyFlowerDef(ThingDef def)
        {
            return def != null && def.plant != null && !def.plant.IsTree && def.GetStatValueAbstract(VVStatDefOf.VV_PlantHoneyGatherYield) > 0f;
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
                if (!VVThingDefOf.Plant_Cotton.CanEverPlantAt(cell, map, out _, canWipePlantsExceptTree: true, checkMapTemperature: false).Accepted) { continue; }

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
                if (thing is Building || thing.def.IsBlueprint || thing.def.IsFrame) { return false; }
            }

            return VVThingDefOf.Plant_Cotton.CanEverPlantAt(cell, map, out _, canWipePlantsExceptTree: true, checkMapTemperature: false).Accepted;
        }

        public void ExposeData()
        {
            Scribe_Collections.Look(ref _cottonCells, "cottonCells", LookMode.Value);
        }
    }
}
