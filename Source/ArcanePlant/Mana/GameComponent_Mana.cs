using System.Collections.Generic;
using System.Linq;
using Verse;

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
        public IEnumerable<CompMana> AllManaComps
        {
            get
            {
                for (int i = 0; i < _manaCompCache.Count; ++i)
                {
                    if (_manaCompCache[i] == null || _manaCompCache[i].parent == null || _manaCompCache[i].parent.Destroyed)
                    {
                        _manaCompCache.RemoveAt(i);
                        i--;

                        continue;
                    }

                    yield return _manaCompCache[i];

                }
            }
        }

        public IEnumerable<ThingWithComps> AllThingsUsingMana
        {
            get
            {
                for (int i = 0; i < _thingWithManaComps.Count; ++i)
                {
                    if (_thingWithManaComps[i] == null || _thingWithManaComps[i].Destroyed)
                    {
                        _thingWithManaComps.RemoveAt(i);
                        i--;

                        continue;
                    }

                    yield return _thingWithManaComps[i];
                }
            }
        }

        private Game _game;
        private List<ThingWithComps> _thingWithManaComps;
        private List<CompMana> _manaCompCache;

        public IEnumerable<Pawn> GetFairyficatedPawns(Pawn pawn) => _fairyficated.Where(v => v.linker == pawn).Select(v => v.fairy);
        private List<FairyficatedPawn> _fairyficated = new List<FairyficatedPawn>();

        // 엔딩 퀘스트 〈만개하는 여명〉 상태. 새 게임 컴포넌트를 만들지 않고 이 컴포넌트에서 보관·저장하고 틱을 처리한다.
        public ViviEndingQuest EndingQuest => _endingQuest;
        private ViviEndingQuest _endingQuest = new ViviEndingQuest();

        public GameComponent_Mana(Game game)
        {
            _game = game;
            _thingWithManaComps = new List<ThingWithComps>();
            _manaCompCache = new List<CompMana>();
            _fairyficated = new List<FairyficatedPawn>();
        }

        public override void ExposeData()
        {
            if (Scribe.mode == LoadSaveMode.Saving)
            {
                _thingWithManaComps.RemoveAll(v => v.DestroyedOrNull());
            }

            Scribe_Collections.Look(ref _thingWithManaComps, "_thingWithManaComps", LookMode.Reference);
            Scribe_Collections.Look(ref _fairyficated, "fairyficated", LookMode.Deep);
            Scribe_Deep.Look(ref _endingQuest, "endingQuest");

            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                _manaCompCache.Clear();

                _thingWithManaComps.RemoveAll(v => v.DestroyedOrNull());
                for (int i = 0; i < _thingWithManaComps.Count; ++i)
                {
                    var thing = _thingWithManaComps[i];
                    var compMana = thing.GetComp<CompMana>();
                    if (compMana != null)
                    {
                        _manaCompCache.Add(compMana);
                    }
                    else
                    {
                        Log.Warning($"failed to find CompMana for thing {thing}; it registered on GameComponent_Mana before save. it may cause bugs about updating mana.");

                        _thingWithManaComps.RemoveAt(i);
                        i--;
                    }
                }

                if (_fairyficated == null)
                {
                    _fairyficated = new List<FairyficatedPawn>();
                }

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

        // 화면 섬광 제거 등 엔딩 연출의 실시간 처리를 담당한다(일시정지와 무관).
        public override void GameComponentUpdate()
        {
            _endingQuest?.Update();
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
    }
}
