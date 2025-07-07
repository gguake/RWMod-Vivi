using System.Collections.Generic;
using Verse;

namespace VVRace
{
    public class ArcaneSeedMapComponent : MapComponent
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

        public ArcaneSeedMapComponent(Map map) : base(map)
        {
            _arcaneSeeds = new List<ThingWithComps>();

            map.events.TerrainChanged += (cell) =>
            {
                foreach (var seed in _arcaneSeeds)
                {

                }
            };
        }

        public override void ExposeData()
        {
            Scribe_Collections.Look(ref _arcaneSeeds, "arcaneSeeds", LookMode.Reference);
        }

        public void Register(ThingWithComps seed)
        {
            _arcaneSeeds.Add(seed);
        }

        public void Unregister(ThingWithComps seed)
        {
            _arcaneSeeds.Remove(seed);
        }
    }
}
