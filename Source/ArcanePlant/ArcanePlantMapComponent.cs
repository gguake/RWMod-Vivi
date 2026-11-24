using System.Collections.Generic;
using System.Linq;
using Verse;

namespace VVRace
{
    public class ArcanePlantMapComponent : MapComponent
    {
        private static int _cachedComponentIndex = -1;
        public static ArcanePlantMapComponent Of(Map map)
        {
            if (map == null) { return null; }

            var components = map.components;
            var index = _cachedComponentIndex;
            if ((uint)index < (uint)components.Count && components[index] is ArcanePlantMapComponent cached)
            {
                return cached;
            }

            for (int i = 0; i < components.Count; ++i)
            {
                if (components[i] is ArcanePlantMapComponent found)
                {
                    _cachedComponentIndex = i;
                    return found;
                }
            }

            return null;
        }

        public IEnumerable<ThingWithComps> ArcaneSeeds
        {
            get
            {
                PruneArcaneSeeds();
                return _arcaneSeeds;
            }
        }
        internal List<ThingWithComps> ArcaneSeedsList
        {
            get
            {
                PruneArcaneSeeds();
                return _arcaneSeeds;
            }
        }
        public bool HasArcaneSeeds
        {
            get
            {
                if (_arcaneSeeds.Count == 0) { return false; }
                PruneArcaneSeeds();
                return _arcaneSeeds.Count > 0;
            }
        }
        private void PruneArcaneSeeds()
        {
            for (int i = _arcaneSeeds.Count - 1; i >= 0; --i)
            {
                var v = _arcaneSeeds[i];
                if (v == null || v.Destroyed || v.MapHeld != map)
                {
                    _arcaneSeeds.RemoveAt(i);
                }
            }
        }
        private List<ThingWithComps> _arcaneSeeds;

        public ArcanePlant GetArcanePlantAtCell(IntVec3 cell)
        {
            if (_arcanePlants.TryGetValue(cell, out var plant))
            {
                if (!plant.Spawned || plant.Destroyed)
                {
                    _arcanePlants.Remove(cell);
                    return null;
                }

                return plant;
            }

            return null;
        }
        private Dictionary<IntVec3, ArcanePlant> _arcanePlants;

        public ArcanePlantPot GetArcanePlantPot(IntVec3 cell)
        {
            if (_arcanePlantPots.TryGetValue(cell, out var pot))
            {
                if (!pot.Spawned || pot.Destroyed)
                {
                    _arcanePlantPots.Remove(cell);
                    return null;
                }

                return pot;
            }

            return null;
        }
        private Dictionary<IntVec3, ArcanePlantPot> _arcanePlantPots;

        // IntGrid는 범위를 검사하지 않아, 맵 밖 셀은 예외를 내거나 다른 줄의 셀을 가리킨다.
        public bool CheckVaccumResistance(IntVec3 cell) => cell.InBounds(map) && _plantVaccumOverrideGrid[cell] > 0;
        private IntGrid _plantVaccumOverrideGrid;

        private Dictionary<Room, ThingDef> _scentRooms;
        private bool _scentRoomsDirty;
        private long _nextScentTick = long.MinValue;
        private int _lastScentTick = -1;
        private readonly Dictionary<ArcanePlantPot, CellRect> _potRegisteredRects = new Dictionary<ArcanePlantPot, CellRect>();

        public bool HasAnyArcanePlant => _arcanePlants.Count > 0;

        public ArcanePlantMapComponent(Map map) : base(map)
        {
            _arcaneSeeds = new List<ThingWithComps>();
            _arcanePlants = new Dictionary<IntVec3, ArcanePlant>();
            _arcanePlantPots = new Dictionary<IntVec3, ArcanePlantPot>();

            _plantVaccumOverrideGrid = new IntGrid(map);

            _scentRooms = new Dictionary<Room, ThingDef>();
            _scentRoomsDirty = true;

            map.events.RegionsRoomsChanged += () => _scentRoomsDirty = true;
            map.events.RoofChanged += (_) => _scentRoomsDirty = true;

            map.events.TerrainChanged += (cell) =>
            {
                if (HasArcaneSeeds)
                {
                    bool anyContains = false;
                    for (int i = 0; i < _arcaneSeeds.Count; i++)
                    {
                        var compSeed = _arcaneSeeds[i].GetComp<CompArcaneSeed>();
                        if (compSeed != null && compSeed.SeedlingCells.Contains(cell))
                        {
                            anyContains = true;
                            break;
                        }
                    }

                    if (anyContains && !ArcanePlantUtility.CanPlaceArcanePlantToCell(map, cell, VVThingDefOf.VV_ArcanePlantSeedling))
                    {
                        for (int i = 0; i < _arcaneSeeds.Count; i++)
                        {
                            _arcaneSeeds[i].GetComp<CompArcaneSeed>()?.SeedlingCells.Remove(cell);
                        }
                    }
                }

                var plant = GetArcanePlantAtCell(cell);
                if (plant != null)
                {
                    if (!ArcanePlantUtility.CanPlaceArcanePlantToCell(map, cell, plant.def, plant))
                    {
                        if (plant.def.Minifiable)
                        {
                            plant.MinifyAndDropDirect();
                        }
                        else
                        {
                            plant.Destroy();
                        }
                    }
                }
            };
        }

        public override void ExposeData()
        {
            Scribe_Collections.Look(ref _arcaneSeeds, "arcaneSeeds", LookMode.Reference);
        }

        public override void MapComponentTick()
        {
            if (_scentRoomsDirty)
            {
                RebuildScentRooms();
                _nextScentTick = long.MinValue;
            }

            if (_scentRooms.Count == 0) { return; }

            var ticks = GenTicks.TicksGame;
            if (ticks < _lastScentTick)
            {
                _nextScentTick = long.MinValue;
            }
            _lastScentTick = ticks;
            if (_nextScentTick > ticks) { return; }

            // int.MaxValue 경계에서의 int 오버플로를 피하기 위해 long으로 다음 확산 틱을 계산한다.
            long interval = ScentUtility.DiffusionIntervalTicks;
            long nextMin = long.MaxValue;
            foreach (var kv in _scentRooms)
            {
                long rem = ((long)ticks + kv.Key.ID) % interval;
                if (rem < 0) { rem += interval; }
                if (rem == 0)
                {
                    if (kv.Key.Vacuum < 0.5f)
                    {
                        ScentUtility.ApplyScentToRoom(kv.Key, kv.Value);
                    }
                    long candidate = ticks + interval;
                    if (candidate < nextMin) { nextMin = candidate; }
                }
                else
                {
                    long candidate = ticks + (interval - rem);
                    if (candidate < nextMin) { nextMin = candidate; }
                }
            }

            _nextScentTick = nextMin;
        }

        private void RebuildScentRooms()
        {
            _scentRoomsDirty = false;
            _scentRooms.Clear();

            HashSet<Room> conflicted = null;
            foreach (var plant in _arcanePlants.Values)
            {
                if (plant.Destroyed || !plant.Spawned) { continue; }
                if (!ScentUtility.IsScentFlower(plant.def)) { continue; }

                var room = plant.Position.GetRoom(map);
                if (room == null || room.PsychologicallyOutdoors) { continue; }

                if (_scentRooms.TryGetValue(room, out var existing))
                {
                    if (existing != plant.def)
                    {
                        if (conflicted == null) { conflicted = new HashSet<Room>(); }
                        conflicted.Add(room);
                    }
                }
                else
                {
                    _scentRooms.Add(room, plant.def);
                }
            }

            if (conflicted != null)
            {
                foreach (var room in conflicted)
                {
                    _scentRooms.Remove(room);
                }
            }
        }

        public void Notify_ArcaneSeedPlantReserved(ThingWithComps seed)
        {
            if (!_arcaneSeeds.Contains(seed))
            {
                _arcaneSeeds.Add(seed);
            }
        }

        public void Notify_ArcaneSeedPlantCanceled(ThingWithComps seed)
        {
            _arcaneSeeds.Remove(seed);
        }

        public void Notify_ArcanePlantSpawned(ArcanePlant plant)
        {
            _arcanePlants.Add(plant.Position, plant);

            if (ScentUtility.IsScentFlower(plant.def))
            {
                _scentRoomsDirty = true;
            }
        }

        // 디스폰 없이 Position만 바꾸면 이전 칸의 사전 항목이 남으므로 직접 갱신한다.
        public void Notify_ArcanePlantMoved(ArcanePlant plant, IntVec3 oldCell)
        {
            if (_arcanePlants.TryGetValue(oldCell, out var existing) && existing == plant)
            {
                _arcanePlants.Remove(oldCell);
            }

            _arcanePlants[plant.Position] = plant;

            if (ScentUtility.IsScentFlower(plant.def))
            {
                _scentRoomsDirty = true;
            }
        }

        public void Notify_StalitflowerSpawned(ArcanePlant plant)
        {
            if (!ModsConfig.OdysseyActive) { return; }

            foreach (var cell in GenRadial.RadialCellsAround(plant.Position, ArcanePlant_Starlitflower.VaccumResistRange, true))
            {
                if (!cell.InBounds(map)) { continue; }

                _plantVaccumOverrideGrid[cell]++;
            }
        }

        public void Notify_ArcanePlantDespawned(ArcanePlant plant)
        {
            _arcanePlants.Remove(plant.Position);

            if (ScentUtility.IsScentFlower(plant.def))
            {
                _scentRoomsDirty = true;
            }
        }

        public void Notify_StalitflowerDespawned(ArcanePlant plant)
        {
            if (!ModsConfig.OdysseyActive) { return; }

            foreach (var cell in GenRadial.RadialCellsAround(plant.Position, ArcanePlant_Starlitflower.VaccumResistRange, true))
            {
                if (!cell.InBounds(map)) { continue; }

                _plantVaccumOverrideGrid[cell]--;
            }
        }

        public void Notify_ArcanePlantPotSpawned(ArcanePlantPot pot)
        {
            var rect = pot.OccupiedRect();
            _potRegisteredRects[pot] = rect;
            foreach (var cell in rect)
            {
                _arcanePlantPots.Add(cell, pot);
            }
        }

        public void Notify_ArcanePlantPotDespawned(ArcanePlantPot pot)
        {
            if (!_potRegisteredRects.TryGetValue(pot, out var rect))
            {
                rect = pot.OccupiedRect();
            }
            else
            {
                _potRegisteredRects.Remove(pot);
            }

            foreach (var cell in rect)
            {
                if (_arcanePlantPots.TryGetValue(cell, out var existing) && existing == pot)
                {
                    _arcanePlantPots.Remove(cell);
                }
            }
        }
    }
}
