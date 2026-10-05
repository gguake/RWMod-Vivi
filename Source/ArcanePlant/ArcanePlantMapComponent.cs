using System.Collections.Generic;
using System.Linq;
using Verse;

namespace VVRace
{
    public class ArcanePlantMapComponent : MapComponent
    {
        public IEnumerable<ThingWithComps> ArcaneSeeds
        {
            get
            {
                _arcaneSeeds.RemoveWhere(v => v == null || v.Destroyed || v.MapHeld != map);
                return _arcaneSeeds;
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

        public bool CheckVaccumResistance(IntVec3 cell) => _plantVaccumOverrideGrid[cell] > 0;
        private IntGrid _plantVaccumOverrideGrid;

        private Dictionary<Room, ThingDef> _scentRooms;
        private bool _scentRoomsDirty;

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

            map.events.TerrainChanged += (cell) =>
            {
                var arcaneSeeds = ArcaneSeeds;
                if (arcaneSeeds.Any(v => v.GetComp<CompArcaneSeed>().SeedlingCells.Contains(cell)))
                {
                    if (!ArcanePlantUtility.CanPlaceArcanePlantToCell(map, cell, VVThingDefOf.VV_ArcanePlantSeedling))
                    {
                        foreach (var seed in _arcaneSeeds)
                        {
                            seed.GetComp<CompArcaneSeed>().SeedlingCells.Remove(cell);
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
            }

            if (_scentRooms.Count == 0) { return; }

            var ticks = GenTicks.TicksGame;
            foreach (var kv in _scentRooms)
            {
                if ((ticks + kv.Key.ID) % ScentUtility.DiffusionIntervalTicks != 0) { continue; }

                if (kv.Key.Vacuum >= 0.5f) { continue; }

                ScentUtility.ApplyScentToRoom(kv.Key, kv.Value);
            }
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
            _arcaneSeeds.Add(seed);
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

        public void Notify_StalitflowerSpawned(ArcanePlant plant)
        {
            if (!ModsConfig.OdysseyActive) { return; }

            foreach (var cell in GenRadial.RadialCellsAround(plant.Position, ArcanePlant_Starlitflower.VaccumResistRange, true))
            {
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
                _plantVaccumOverrideGrid[cell]--;
            }
        }

        public void Notify_ArcanePlantPotSpawned(ArcanePlantPot pot)
        {
            foreach (var cell in pot.OccupiedRect())
            {
                _arcanePlantPots.Add(cell, pot);
            }
        }

        public void Notify_ArcanePlantPotDespawned(ArcanePlantPot pot)
        {
            _arcanePlantPots.RemoveAll(kv => kv.Value == pot);
        }
    }
}
