using System.Collections.Generic;
using Verse;

namespace VVRace
{
    public class GameComponent_Mana : GameComponent
    {
        public IEnumerable<CompMana> AllManaComps => _manaCompCache;

        private Game _game;
        private List<ThingWithComps> _thingWithManaComps;
        private List<CompMana> _manaCompCache;

        public GameComponent_Mana(Game game)
        {
            _game = game;
            _thingWithManaComps = new List<ThingWithComps>();
            _manaCompCache = new List<CompMana>();
        }

        public override void ExposeData()
        {
            Scribe_Collections.Look(ref _thingWithManaComps, "_thingWithManaComps", LookMode.Reference);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                _manaCompCache.Clear();

                foreach (var thing in _thingWithManaComps)
                {
                    var compMana = thing.GetComp<CompMana>();
                    if (compMana != null)
                    {
                        _manaCompCache.Add(compMana);
                    }
                    else
                    {
                        Log.Warning($"failed to find CompMana for thing {thing}; it registered on GameComponent_Mana before save. it may cause bugs about updating mana.");
                        _thingWithManaComps.Remove(thing);
                    }
                }
            }
        }

        public void RegisterCompMana(CompMana comp)
        {
            _manaCompCache.Add(comp);
            _thingWithManaComps.Add(comp.parent);
        }

        public void UnregisterCompMana(CompMana comp)
        {
            _thingWithManaComps.Remove(comp.parent);
            _manaCompCache.Remove(comp);
        }
    }
}
