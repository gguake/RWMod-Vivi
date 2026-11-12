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

        public int FlowerCount
        {
            get
            {
                RefreshFlowerCounts();
                return _flowerCount;
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

        public static bool IsHoneyFlowerDef(ThingDef def)
        {
            if (def == null || def.plant == null || def.plant.IsTree) { return false; }

            return GameComponent_Mana.HasHoneyYield(def);
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
