using System.Collections.Generic;
using System.Linq;
using Verse;
using Verse.AI.Group;

namespace VVRace
{
    public class FairyficatedPawn : IExposable
    {
        public Pawn linker;
        public Pawn fairy;

        public void ExposeData()
        {
            Scribe_References.Look(ref linker, "linker");
            Scribe_Deep.Look(ref fairy, "fairy");
        }
    }

    public class GameComponent_Mana : GameComponent
    {
        public List<CompMana> GetManaCompsOn(Map map)
        {
            var internalList = GetManaCompsOnInternal(map);
            return internalList.Count > 0 ? new List<CompMana>(internalList) : new List<CompMana>();
        }

        internal List<CompMana> GetManaCompsOnInternal(Map map)
        {
            var ticksGame = GenTicks.TicksGame;
            if (_manaCompsByMapTick != ticksGame)
            {
                _manaCompsByMapTick = ticksGame;
                foreach (var list in _manaCompsByMap.Values)
                {
                    list.Clear();
                }

                _manaComps.RemoveWhere(v => v.parent == null || v.parent.Destroyed);

                foreach (var comp in _manaComps)
                {
                    var rootMap = comp.parent.SpawnedParentOrMe?.Map;
                    if (rootMap == null) { continue; }

                    if (!_manaCompsByMap.TryGetValue(rootMap, out var comps))
                    {
                        comps = new List<CompMana>();
                        _manaCompsByMap.Add(rootMap, comps);
                    }

                    comps.Add(comp);
                }

                if (_manaCompsByMap.Count > 0 && Current.Game != null)
                {
                    _tmpStaleMaps.Clear();
                    var activeMaps = Current.Game.Maps;
                    foreach (var kv in _manaCompsByMap)
                    {
                        if (!activeMaps.Contains(kv.Key))
                        {
                            _tmpStaleMaps.Add(kv.Key);
                        }
                    }
                    for (int i = 0; i < _tmpStaleMaps.Count; i++)
                    {
                        _manaCompsByMap.Remove(_tmpStaleMaps[i]);
                    }
                    _tmpStaleMaps.Clear();
                }
            }

            return _manaCompsByMap.TryGetValue(map, out var result) ? result : EmptyManaComps;
        }

        private static readonly List<CompMana> EmptyManaComps = new List<CompMana>();
        private readonly List<Map> _tmpStaleMaps = new List<Map>();

        private Game _game;

        private readonly HashSet<CompMana> _manaComps = new HashSet<CompMana>();
        private readonly Dictionary<Map, List<CompMana>> _manaCompsByMap = new Dictionary<Map, List<CompMana>>();
        private int _manaCompsByMapTick = -1;

        public IEnumerable<Pawn> GetFairyficatedPawns(Pawn pawn) => _fairyficated.Where(v => v.linker == pawn).Select(v => v.fairy);
        public int GetFairyficatedPawnCount(Pawn pawn)
        {
            int count = 0;
            for (int i = 0; i < _fairyficated.Count; i++)
            {
                if (_fairyficated[i].linker == pawn)
                {
                    count++;
                }
            }
            return count;
        }
        private List<FairyficatedPawn> _fairyficated = new List<FairyficatedPawn>();

        public IReadOnlyList<Pawn> AsphodelBoundVivis => _asphodelBound;
        private List<Pawn> _asphodelBound = new List<Pawn>();

        public ViviEndingQuest EndingQuest => _endingQuest;
        private ViviEndingQuest _endingQuest = new ViviEndingQuest();

        private static int _cachedIndex = -1;

        public static GameComponent_Mana Of(Game game)
        {
            if (game == null) { return null; }

            var components = game.components;
            var index = _cachedIndex;
            if ((uint)index < (uint)components.Count && components[index] is GameComponent_Mana cached)
            {
                return cached;
            }

            for (int i = 0; i < components.Count; ++i)
            {
                if (components[i] is GameComponent_Mana found)
                {
                    _cachedIndex = i;
                    return found;
                }
            }

            return null;
        }

        public GameComponent_Mana(Game game)
        {
            _game = game;
            _fairyficated = new List<FairyficatedPawn>();
            Hediff_EverflowerAssimilation.ResetRegistry();
            Hediff_EverflowerQueen.ResetRegistry();
        }

        public override void ExposeData()
        {
            Scribe_Collections.Look(ref _fairyficated, "fairyficated", LookMode.Deep);
            Scribe_Collections.Look(ref _asphodelBound, "asphodelBound", LookMode.Deep);
            Scribe_Deep.Look(ref _endingQuest, "endingQuest");

            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                if (_fairyficated == null)
                {
                    _fairyficated = new List<FairyficatedPawn>();
                }

                _asphodelBound ??= new List<Pawn>();
                _asphodelBound.RemoveAll(v => v == null);

                if (_endingQuest == null)
                {
                    _endingQuest = new ViviEndingQuest();
                }
            }
        }

        public override void GameComponentTick()
        {
            _endingQuest?.Tick();
        }

        public override void GameComponentUpdate()
        {
            _endingQuest?.Update();
        }

        public void RegisterCompMana(CompMana comp)
        {
            _manaComps.Add(comp);
        }

        public void UnregisterCompMana(CompMana comp)
        {
            _manaComps.Remove(comp);
        }

        public void RegisterFairy(Pawn linker, Pawn target)
        {
            if (_fairyficated.Any(v => v.linker == linker && v.fairy == target)) { return; }
                
            _fairyficated.Add(new FairyficatedPawn()
            {
                linker = linker,
                fairy = target,
            });
        }

        public void UnregisterFairy(Pawn target)
        {
            _fairyficated.RemoveAll(v => v.fairy == target);
        }

        public void UnregisterAllFairy(Pawn linker)
        {
            _fairyficated.RemoveAll(v => v.linker == linker);
        }

        public void BindToAsphodel(Pawn vivi)
        {
            if (vivi == null || vivi.Dead || vivi.Destroyed || _asphodelBound.Contains(vivi)) { return; }

            ApocryphaFairyChains.RemoveBondHediffSilently(vivi);

            vivi.GetLord()?.Notify_PawnLost(vivi, PawnLostCondition.Vanished);
            if (vivi.Spawned)
            {
                vivi.jobs?.StopAll();
                if (vivi.carryTracker?.CarriedThing != null)
                {
                    vivi.carryTracker.TryDropCarriedThing(vivi.Position, ThingPlaceMode.Near, out _);
                }

                vivi.DeSpawnOrDeselect(DestroyMode.Vanish);
            }
            else
            {
                vivi.holdingOwner?.Remove(vivi);
            }

            if (Find.WorldPawns.Contains(vivi))
            {
                Find.WorldPawns.RemovePawn(vivi);
            }

            _asphodelBound.Add(vivi);
        }

        public void UnbindFromAsphodel(Pawn vivi)
        {
            _asphodelBound.Remove(vivi);
        }
    }
}
