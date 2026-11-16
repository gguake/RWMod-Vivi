using RimWorld;
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
        public List<CompMana> GetManaCompsOn(Map map)
        {
            var ticksGame = GenTicks.TicksGame;
            if (_manaCompsByMapTick != ticksGame)
            {
                _manaCompsByMapTick = ticksGame;
                _manaCompsByMap.Clear();

                // HashSet은 순회 중에 지울 수 없으므로 파괴된 항목은 돌기 전에 걸러낸다.
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
            }

            return _manaCompsByMap.TryGetValue(map, out var result) ? result : EmptyManaComps;
        }

        private static readonly List<CompMana> EmptyManaComps = new List<CompMana>();

        private Game _game;

        // 저장하지 않는다. 새로 만든 물건은 PostPostMake에서, 불러온 물건은 PostLoadInit에서 CompMana가 스스로 등록한다.
        private readonly HashSet<CompMana> _manaComps = new HashSet<CompMana>();
        private readonly Dictionary<Map, List<CompMana>> _manaCompsByMap = new Dictionary<Map, List<CompMana>>();
        private int _manaCompsByMapTick = -1;

        public IEnumerable<Pawn> GetFairyficatedPawns(Pawn pawn) => _fairyficated.Where(v => v.linker == pawn).Select(v => v.fairy);
        private List<FairyficatedPawn> _fairyficated = new List<FairyficatedPawn>();

        public ViviEndingQuest EndingQuest => _endingQuest;
        private ViviEndingQuest _endingQuest = new ViviEndingQuest();

        // 꿀 꽃 판정용 정의별 메모(추상 꿀 채집량 > 0). 추상 스탯은 정의와 이 게임의 시나리오 스탯 배율로만 정해지므로
        // 게임 컴포넌트에 두면 게임이 바뀔 때 함께 새로 만들어진다. 저장하지 않는다.
        private readonly Dictionary<ThingDef, bool> _honeyYieldDefs = new Dictionary<ThingDef, bool>();

        public static bool HasHoneyYield(ThingDef def)
        {
            var component = Of(Current.Game);
            if (component == null) { return ComputeHoneyYield(def); }

            if (!component._honeyYieldDefs.TryGetValue(def, out var result))
            {
                result = ComputeHoneyYield(def);
                component._honeyYieldDefs[def] = result;
            }
            return result;
        }

        private static bool ComputeHoneyYield(ThingDef def) => def.GetStatValueAbstract(VVStatDefOf.VV_PlantHoneyGatherYield) > 0f;

        // 스토리텔러·보스전에서 자주 찾으므로 GetComponent처럼 목록을 앞에서부터 형식 검사하지 않고 마지막으로 찾은 위치를 먼저 본다.
        // 이전 게임을 붙잡지 않도록 컴포넌트가 아니라 위치만 기억하고, 그 위치에 없으면 다시 찾는다.
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
        }

        public override void ExposeData()
        {
            Scribe_Collections.Look(ref _fairyficated, "fairyficated", LookMode.Deep);
            Scribe_Deep.Look(ref _endingQuest, "endingQuest");

            // PostLoadInit 호출 순서는 정해져 있지 않아 물건이 먼저 등록했을 수 있으므로 여기서 등록부를 비우지 않는다.
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
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

        // 화면 섬광 제거 등 실시간 연출은 일시정지 중에도 처리한다.
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
    }
}
