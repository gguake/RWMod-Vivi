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
                _arcaneSeeds.RemoveWhere(v => v.Destroyed || v.MapHeld != map);
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

        public bool HasAnyArcanePlant => _arcanePlants.Count > 0;

        public ArcanePlantMapComponent(Map map) : base(map)
        {
            _arcaneSeeds = new List<ThingWithComps>();
            _arcanePlants = new Dictionary<IntVec3, ArcanePlant>();
            _arcanePlantPots = new Dictionary<IntVec3, ArcanePlantPot>();

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
                        plant.MinifyAndDropDirect();
                    }
                }
            };
        }

        public override void ExposeData()
        {
            Scribe_Collections.Look(ref _arcaneSeeds, "arcaneSeeds", LookMode.Reference);
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
        }

        public void Notify_ArcanePlantDespawned(ArcanePlant plant)
        {
            _arcanePlants.Remove(plant.Position);
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
