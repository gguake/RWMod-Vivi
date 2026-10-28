using RimWorld;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using Verse;
using Verse.AI;

namespace VVRace
{
    // 아포크리파 전투 단계. 저장되므로 값을 바꾸지 말 것.
    public enum ApocryphaPhase : byte
    {
        Bound = 1,          // 1페이즈 〈수십억의 굴레〉: 일반 피해 면역, 결속 수치 감소가 목표
        Transforming = 2,   // 결속 0 → 충격파 기절 + 거대화 연출 중
        Unbound = 3,        // 2페이즈 〈해방된 시조의 폭주〉: 피해 감소 99%(공명 중첩으로 완화)
        Dying = 4,          // 토벌됨: 소멸 연출 중
    }

    public class CompProperties_Apocrypha : CompProperties
    {
        // 1페이즈 〈수십억의 굴레〉: 요정 비비 결속
        public long initialBondCount = 1440000000L;
        public long bondReleaseAmount = 360000000L;
        public int releaseStunTicks = 300;
        public IntRange releaseEscortCount = new IntRange(3, 5);

        // 1페이즈: 요정 비비 사출 포격
        public float shellRange = 20f;
        public int shellAimTicks = 240;
        public int shellCooldownTicks = 900;

        // 1페이즈: 멸망꽃/요정꽃 씨앗. 멸망꽃 씨앗은 한 번에 여러 개를 흩뿌린다.
        public int firstDoomSeedDelayTicks = 1500;
        public IntRange doomSeedIntervalTicks = new IntRange(6000, 9000);
        public IntRange doomSeedVolley = new IntRange(2, 3);
        public int maxDoomFlowers = 9;
        public int doomSeedsBeforeFairySeed = 6;
        public IntRange fairySeedIntervalTicks = new IntRange(7500, 10000);
        public int maxFairyFlowers = 2;

        // 1페이즈: 배회와 주기적 접근
        public IntRange approachIntervalTicks = new IntRange(6000, 9000);
        public float approachDistance = 12f;
        public float wanderRadius = 10f;
        public IntRange wanderWaitTicks = new IntRange(600, 1200);

        // 호위 거대 말벌
        public IntRange escortReinforceCount = new IntRange(2, 4);
        public float escortGuardRadius = 10f;
        // 이 반경(보스 기준) 안에서 최근 보스·호위 말벌을 공격한 적은 경호 반경 밖이어도 반격한다.
        public float escortRetaliationRadius = 25f;

        // 보스전 내내 공허 노드 엔딩의 배회 존재처럼 맵 외곽에서 거대 말벌이 꾸준히 몰려온다.
        // 한 시간마다 소규모로 들어오며, 살아 있는 말벌의 전투력 합이 위협 포인트에 따른 상한에 도달하면 증원을 멈춘다(바닐라 QuestPart_WanderingEntities와 같은 곡선).
        public int edgeHornetIntervalTicks = 2500;
        public IntRange edgeHornetGroupSize = new IntRange(1, 2);
        public SimpleCurve edgeHornetMaxPointsByThreatPoints = new SimpleCurve
        {
            new CurvePoint(500f, 750f),
            new CurvePoint(2500f, 1500f),
            new CurvePoint(5000f, 2000f),
            new CurvePoint(10000f, 3000f),
        };

        // 2페이즈 전환 연출: 충격파로 모두 기절한 동안 몸이 VV_Morph 셰이더로 꿈틀거리며 2페이즈 몸으로 뒤바뀐다.
        public int transformTicks = 420;
        public int transformGraphicSwapTicks = 210;
        public int transformStunTicks = 420;

        // 2페이즈 〈해방된 시조의 폭주〉
        public float maxVitality = 9000f;
        public List<float> damageReductionByStacks = new List<float> { 0.99f, 0.5f, 0.3f, 0.15f, 0.05f, 0f };
        public int breathCooldownTicks = 1200;
        public int breathWarmupTicks = 90;
        public float breathRange = 7f;
        public float breathAngle = 50f;
        public float breathToxicSeverity = 0.25f;
        public float breathAcidBurnDamage = 8f;
        public int meleeIntervalTicks = 60;
        public int smashIntervalTicks = 30;
        public float smashDamage = 400f;

        // 영원꽃 찬탈(채널링): 실패 조건
        public int channelTicksBound = 5000;
        public int channelTicksUnbound = 3600;
        public float channelRangeAltar = 3.9f;
        public float channelRangeEverflower = 1.9f;

        public CompProperties_Apocrypha()
        {
            compClass = typeof(CompApocrypha);
        }
    }

    // 아포크리파 본체 컴프. 페이즈/결속 수치/생명력 풀/능력 타이머/채널링을 관리한다.
    // [계약] public 시그니처는 다른 모듈이 호출하므로 바꾸지 말 것.
    // 모든 타이머는 매 틱 도는 CompTick에서 처리한다(동물 폰의 CompTickInterval은 15틱 간격이라 쓰지 않는다).
    public class CompApocrypha : ThingComp
    {
        private static readonly Color AimBeamColor = new Color(1f, 0.86f, 0.45f, 0.85f);
        private static readonly Color ChannelBeamColor = new Color(1f, 0.32f, 0.82f, 1f);
        private static readonly Color DamageTextColor = new Color(1f, 0.72f, 0.4f);

        private const int ChannelContactCheckInterval = 10;
        private const int ShellTargetScanInterval = 30;
        private const int BreathTargetScanInterval = 20;
        private const int BlockedTextMoteInterval = 60;
        private const int DamageTextInterval = 30;
        private const int SeedRetryTicks = 600;
        private const int ChannelInterruptMessageMinTicks = 120;
        private const float BreathFrontSearchAngle = 45f;

        private static readonly List<Thing> _tmpTargets = new List<Thing>();
        private static readonly List<Thing> _tmpCellThings = new List<Thing>();

        private static ThingDef _doomFlowerDef;
        private static ThingDef _fairyFlowerDef;
        private static ThingDef DoomFlowerDef => _doomFlowerDef ?? (_doomFlowerDef = DefDatabase<ThingDef>.GetNamedSilentFail("VV_DoomFlower"));
        private static ThingDef FairyFlowerDef => _fairyFlowerDef ?? (_fairyFlowerDef = DefDatabase<ThingDef>.GetNamedSilentFail("VV_FairyFlower"));

        // 저장되는 상태
        private ApocryphaPhase _phase = ApocryphaPhase.Bound;
        private bool _initialized;
        private long _bondCount;
        private float _vitality;
        private float _maxVitality;
        private int _resonanceStacks;
        private int _transformStartTick = -1;
        private bool _transformSwapped;
        private int _channelTicks;
        private bool _pendingSteal;
        private bool _pendingDeath;
        private int _shellReadyTick;
        private IntVec3 _aimCell = IntVec3.Invalid;
        private int _aimEndTick = -1;
        private int _breathReadyTick;
        private IntVec3 _breathCell = IntVec3.Invalid;
        private int _breathEndTick = -1;
        private int _nextDoomSeedTick = -1;
        private int _nextFairySeedTick = -1;
        private int _doomSeedsLaunched;
        private int _nextApproachTick;
        private int _nextEdgeHornetTick;
        private List<Pawn> _edgeHornets = new List<Pawn>();
        private int _nextMeleeTick;

        // 저장하지 않는 연출/디버그 상태
        private int _lastBlockedTextTick = -99999;
        private float _pendingDamageText;
        private int _lastDamageTextTick = -99999;
        private int _lastChannelInterruptMessageTick = -99999;
        private int _lastStunImmuneTextTick = -99999;
        private bool _devForceChannel;

        // 기믹(결속 해방·공명 파동·마나 파동 의식)에 의한 기절을 적용하는 동안만 true. 그 외의 기절은 ApocryphaPatch가 StunFor에서 막는다.
        internal static bool ApplyingGimmickStun { get; private set; }

        public CompProperties_Apocrypha Props => (CompProperties_Apocrypha)props;

        public Pawn Pawn => (Pawn)parent;

        public ApocryphaPhase Phase => _phase;

        // 요정 비비 결속 수치(초기 1,440,000,000). Hediff_ApocryphaFairyBond 라벨에 표시된다.
        public long BondCount => _bondCount;
        public long InitialBondCount => Props.initialBondCount;

        // 2페이즈 생명력 풀. 모든 피해는 신체 부위 대신 이 값에서 차감된다.
        public float Vitality => _vitality;
        public float MaxVitality => _maxVitality > 0f ? _maxVitality : Props.maxVitality;

        // 2페이즈 받는 피해 감소율(0~1)과 공명 파동 중첩 수.
        public int ResonanceStacks => _resonanceStacks;
        public float DamageReduction
        {
            get
            {
                if (_phase != ApocryphaPhase.Unbound) { return 1f; }

                var table = Props.damageReductionByStacks;
                if (table.NullOrEmpty()) { return 0f; }

                return table[Mathf.Clamp(_resonanceStacks, 0, table.Count - 1)];
            }
        }

        public bool IsChanneling => parent.Spawned && Pawn.CurJobDef == ApocryphaJobDefOf.VV_ApocryphaChannel;
        public int ChannelDurationTicks => _phase == ApocryphaPhase.Unbound ? Props.channelTicksUnbound : Props.channelTicksBound;
        public float ChannelProgress => IsChanneling ? Mathf.Clamp01((float)_channelTicks / ChannelDurationTicks) : 0f;
        public int ChannelTicksLeft => Mathf.Max(0, ChannelDurationTicks - _channelTicks);

        public int DoomSeedsLaunched => _doomSeedsLaunched;

        public bool IsActive => _phase == ApocryphaPhase.Bound || _phase == ApocryphaPhase.Unbound;

        // 2페이즈 전환(모핑) 시작부터 경과한 시간(초)과 진행도(0~1). 렌더 서브워커(병렬)도 읽으므로 저장 필드와 현재 틱만 쓴다.
        public float TransformAgeSecs => _transformStartTick < 0 ? 0f : Mathf.Max(0, GenTicks.TicksGame - _transformStartTick) / 60f;
        public float TransformProgress => _transformStartTick < 0 ? 0f : Mathf.Clamp01((GenTicks.TicksGame - _transformStartTick) / (float)Mathf.Max(1, Props.transformTicks));

        // 엔딩 퀘스트가 지금 이 보스와 싸우는 중인지(보스 단계이고 퀘스트가 기억하는 보스가 이 폰).
        // 증원·씨앗·사출 포격·채널링(찬탈)은 이때만 진행한다. 엔딩이 끝났거나 퀘스트와 무관하게 생성된 아포크리파는 배회만 한다.
        public bool IsQuestBossFight
        {
            get
            {
                var quest = ViviEndingQuest.Current;
                return quest != null && quest.Stage == ViviEndingStage.Boss && quest.Boss == Pawn;
            }
        }
        public bool IsAiming => _aimEndTick >= 0;
        public IntVec3 AimCell => _aimCell;
        public bool IsBreathing => _breathEndTick >= 0;
        public IntVec3 BreathCell => _breathCell;
        public int NextApproachTick => _nextApproachTick;
        public bool DevForceChannel => _devForceChannel;

        // 링크/조준선의 보스 쪽 끝. 몸통이 칸 중심보다 위쪽에 그려지므로 연결선 모듈(ApocryphaGimmicks)과 같은 높이로 올린다.
        public Vector3 BodyCenter => Pawn.DrawPos + BodyCenterOffset;

        private static readonly Vector3 BodyCenterOffset = new Vector3(0f, 0f, 1.5f);

        #region 초기화/저장
        public override void PostSpawnSetup(bool respawningAfterLoad)
        {
            base.PostSpawnSetup(respawningAfterLoad);

            if (!_initialized)
            {
                InitializeState();
            }

            SyncLifeStage();
            SyncPhaseHediffs();

            if (_phase == ApocryphaPhase.Transforming)
            {
                PrepareMorph();
            }
        }

        // 모핑 재질을 메인 스레드에서 준비하고 몸통 재질을 다시 고르게 한다(전환 시작·끝, 전환 중 불러오기).
        private void PrepareMorph()
        {
            ApocryphaMorphMaterials.EnsureInitialized(Props.transformTicks / 60f);
            Pawn.Drawer?.renderer?.SetAllGraphicsDirty();
        }

        private void InitializeState()
        {
            var now = GenTicks.TicksGame;

            _initialized = true;
            _phase = ApocryphaPhase.Bound;
            _bondCount = Props.initialBondCount;
            _maxVitality = Props.maxVitality;
            _vitality = _maxVitality;
            _resonanceStacks = 0;

            _nextDoomSeedTick = now + Props.firstDoomSeedDelayTicks;
            _nextFairySeedTick = -1;
            _doomSeedsLaunched = 0;
            _nextApproachTick = now + Props.approachIntervalTicks.RandomInRange;
            _nextEdgeHornetTick = now + Props.edgeHornetIntervalTicks;

            // 등장 직후에는 잠시 숨을 고른 뒤 포격을 시작한다.
            _shellReadyTick = now + 600;
            _breathReadyTick = now;
        }

        // 페이즈 그래픽은 생애 단계로 바꾼다(0: 1페이즈, 1: 2페이즈). 잠금은 저장되지만 불러온 뒤에도 한번 맞춰 둔다.
        private void SyncLifeStage()
        {
            var index = (_phase == ApocryphaPhase.Bound || (_phase == ApocryphaPhase.Transforming && !_transformSwapped)) ? 0 : 1;
            var maxIndex = Pawn.RaceProps.lifeStageAges.Count - 1;
            Pawn.ageTracker.LockCurrentLifeStageIndex(Mathf.Clamp(index, 0, maxIndex));
        }

        // 1페이즈에는 결속 헤디프, 2페이즈에는 외피 헤디프만 붙어 있게 한다.
        private void SyncPhaseHediffs()
        {
            SyncHediff(ApocryphaHediffDefOf.VV_ApocryphaFairyBond, _phase == ApocryphaPhase.Bound);
            SyncHediff(ApocryphaHediffDefOf.VV_ApocryphaCarapace, _phase == ApocryphaPhase.Unbound);
        }

        private void SyncHediff(HediffDef def, bool shouldHave)
        {
            var hediff = Pawn.health.hediffSet.GetFirstHediffOfDef(def);
            if (shouldHave && hediff == null)
            {
                Pawn.health.AddHediff(def);
            }
            else if (!shouldHave && hediff != null)
            {
                Pawn.health.RemoveHediff(hediff);
            }
        }

        public override void PostExposeData()
        {
            base.PostExposeData();

            Scribe_Values.Look(ref _phase, "phase", ApocryphaPhase.Bound);
            Scribe_Values.Look(ref _initialized, "initialized", false);
            Scribe_Values.Look(ref _bondCount, "bondCount", 0L);
            Scribe_Values.Look(ref _vitality, "vitality", 0f);
            Scribe_Values.Look(ref _maxVitality, "maxVitality", 0f);
            Scribe_Values.Look(ref _resonanceStacks, "resonanceStacks", 0);
            Scribe_Values.Look(ref _transformStartTick, "transformStartTick", -1);
            Scribe_Values.Look(ref _transformSwapped, "transformSwapped", false);
            Scribe_Values.Look(ref _channelTicks, "channelTicks", 0);
            Scribe_Values.Look(ref _pendingSteal, "pendingSteal", false);
            Scribe_Values.Look(ref _pendingDeath, "pendingDeath", false);
            Scribe_Values.Look(ref _shellReadyTick, "shellReadyTick", 0);
            Scribe_Values.Look(ref _aimCell, "aimCell", IntVec3.Invalid);
            Scribe_Values.Look(ref _aimEndTick, "aimEndTick", -1);
            Scribe_Values.Look(ref _breathReadyTick, "breathReadyTick", 0);
            Scribe_Values.Look(ref _breathCell, "breathCell", IntVec3.Invalid);
            Scribe_Values.Look(ref _breathEndTick, "breathEndTick", -1);
            Scribe_Values.Look(ref _nextDoomSeedTick, "nextDoomSeedTick", -1);
            Scribe_Values.Look(ref _nextFairySeedTick, "nextFairySeedTick", -1);
            Scribe_Values.Look(ref _doomSeedsLaunched, "doomSeedsLaunched", 0);
            Scribe_Values.Look(ref _nextApproachTick, "nextApproachTick", 0);
            Scribe_Values.Look(ref _nextMeleeTick, "nextMeleeTick", 0);

            // 죽은(시체가 된) 말벌은 참조로 저장하지 않는다.
            if (Scribe.mode == LoadSaveMode.Saving)
            {
                _edgeHornets.RemoveAll(v => v == null || v.Destroyed || v.Dead);
            }

            Scribe_Values.Look(ref _nextEdgeHornetTick, "nextEdgeHornetTick", 0);
            Scribe_Collections.Look(ref _edgeHornets, "edgeHornets", LookMode.Reference);

            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                if (_edgeHornets == null)
                {
                    _edgeHornets = new List<Pawn>();
                }

                _edgeHornets.RemoveAll(v => v == null);
            }
        }
        #endregion

        #region 틱
        public override void CompTick()
        {
            base.CompTick();
            if (!parent.Spawned) { return; }

            // 파괴를 동반하는 처리는 피해/작업 콜스택 밖인 다음 틱에서 한다.
            if (_pendingSteal)
            {
                _pendingSteal = false;
                StealEverflower();
                return;
            }

            if (_pendingDeath)
            {
                _pendingDeath = false;
                StartDying();
                return;
            }

            var now = GenTicks.TicksGame;
            switch (_phase)
            {
                case ApocryphaPhase.Bound:
                    TickBound(now);
                    break;

                case ApocryphaPhase.Transforming:
                    TickTransforming(now);
                    break;

                case ApocryphaPhase.Unbound:
                    TickUnbound(now);
                    break;
            }

            if (!parent.Spawned) { return; }

            if (IsActive && parent.IsHashIntervalTick(ChannelContactCheckInterval))
            {
                CheckChannelContact();
            }

            if (IsActive && now >= _nextEdgeHornetTick && IsQuestBossFight)
            {
                _nextEdgeHornetTick = now + Props.edgeHornetIntervalTicks;
                TickEdgeHornets();
            }

            if (_pendingDamageText > 0f && now - _lastDamageTextTick >= DamageTextInterval)
            {
                FlushDamageText(now);
            }
        }

        private void TickBound(int now)
        {
            // 엔딩 보스전이 아니면(엔딩 완료·실패 뒤, 퀘스트와 무관한 개체) 능력 타이머를 돌리지 않는다.
            if (!IsQuestBossFight)
            {
                if (IsAiming)
                {
                    CancelAim();
                }
                return;
            }

            var stunned = Pawn.stances.stunner.Stunned;
            if (!stunned)
            {
                TickSeeds(now);
            }

            TickShell(now, stunned);
        }

        // 공허 노드 엔딩처럼 맵 외곽에서 거대 말벌이 소규모로 꾸준히 들어와 정착지를 습격한다.
        // 이렇게 들어온 말벌 중 살아 있는 것의 전투력 합이 위협 포인트에 따른 상한 이상이면 이번 차례는 건너뛴다.
        private void TickEdgeHornets()
        {
            var map = parent.Map;
            _edgeHornets.RemoveAll(v => v == null || v.Dead || !v.Spawned || v.Map != map);

            var hornetPower = Mathf.Max(1f, VVPawnKindDefOf.VV_TitanicHornet.combatPower);
            var maxPoints = Props.edgeHornetMaxPointsByThreatPoints.Evaluate(StorytellerUtility.DefaultThreatPointsNow(map));
            var room = Mathf.FloorToInt((maxPoints - _edgeHornets.Count * hornetPower) / hornetPower);
            if (room <= 0) { return; }

            _edgeHornets.AddRange(ApocryphaUtility.SpawnEdgeHornets(map, Mathf.Min(room, Props.edgeHornetGroupSize.RandomInRange)));
        }

        private void TickSeeds(int now)
        {
            if (_nextDoomSeedTick >= 0 && now >= _nextDoomSeedTick)
            {
                var room = Props.maxDoomFlowers - CountOnMap(DoomFlowerDef);
                var launched = room > 0 ? ApocryphaGimmicks.TryLaunchDoomSeeds(Pawn, Mathf.Min(room, Props.doomSeedVolley.RandomInRange)) : 0;
                if (launched > 0)
                {
                    _doomSeedsLaunched += launched;
                    _nextDoomSeedTick = now + Props.doomSeedIntervalTicks.RandomInRange;
                }
                else
                {
                    // 착탄 칸이 없거나 이미 최대 개수면 잠시 뒤 다시 시도한다.
                    _nextDoomSeedTick = now + SeedRetryTicks;
                }
            }

            if (_doomSeedsLaunched < Props.doomSeedsBeforeFairySeed) { return; }

            if (_nextFairySeedTick < 0)
            {
                _nextFairySeedTick = now + Props.fairySeedIntervalTicks.RandomInRange;
            }
            else if (now >= _nextFairySeedTick)
            {
                if (CountOnMap(FairyFlowerDef) < Props.maxFairyFlowers && ApocryphaGimmicks.TryLaunchFairySeed(Pawn))
                {
                    _nextFairySeedTick = now + Props.fairySeedIntervalTicks.RandomInRange;
                }
                else
                {
                    _nextFairySeedTick = now + SeedRetryTicks;
                }
            }
        }

        private void TickShell(int now, bool stunned)
        {
            if (IsAiming)
            {
                // 기절하면 조준이 흐트러진다.
                if (stunned)
                {
                    CancelAim();
                    _shellReadyTick = now + 120;
                    return;
                }

                if (now >= _aimEndTick)
                {
                    FireShell(now);
                }
                return;
            }

            if (stunned || now < _shellReadyTick || IsChanneling || _pendingSteal) { return; }
            if (!parent.IsHashIntervalTick(ShellTargetScanInterval)) { return; }

            if (TryFindShellTarget(out var cell))
            {
                _aimCell = cell;
                _aimEndTick = now + Props.shellAimTicks;
                Pawn.rotationTracker.FaceCell(cell);
                InterruptCurrentJob();
            }
        }

        private void FireShell(int now)
        {
            var cell = _aimCell;
            CancelAim();
            _shellReadyTick = now + Props.shellCooldownTicks;

            if (cell.IsValid && cell.InBounds(parent.Map))
            {
                ApocryphaGimmicks.TryFireFairyShell(Pawn, cell);
            }
        }

        private void CancelAim()
        {
            _aimEndTick = -1;
            _aimCell = IntVec3.Invalid;
        }

        // 반경 20 안 시야에 들어온 플레이어 세력 폰을 우선 노리고, 없으면 플레이어 포탑을 노린다.
        private bool TryFindShellTarget(out IntVec3 cell)
        {
            cell = IntVec3.Invalid;

            var map = parent.Map;
            var origin = parent.Position;
            var rangeSquared = Props.shellRange * Props.shellRange;

            _tmpTargets.Clear();
            foreach (var target in map.mapPawns.SpawnedPawnsInFaction(Faction.OfPlayer))
            {
                if (target.Dead || target.Downed || target.IsPsychologicallyInvisible()) { continue; }
                if ((target.Position - origin).LengthHorizontalSquared > rangeSquared) { continue; }
                if (!GenSight.LineOfSight(origin, target.Position, map, skipFirstCell: true)) { continue; }

                _tmpTargets.Add(target);
            }

            if (_tmpTargets.Count == 0)
            {
                foreach (var building in map.listerBuildings.allBuildingsColonist)
                {
                    if (!(building is Building_Turret) && !(building is ArcanePlant_Turret)) { continue; }
                    if ((building.Position - origin).LengthHorizontalSquared > rangeSquared) { continue; }
                    if (!GenSight.LineOfSightToThing(origin, building, map, skipFirstCell: true)) { continue; }

                    _tmpTargets.Add(building);
                }
            }

            if (_tmpTargets.Count > 0)
            {
                cell = _tmpTargets.RandomElement().Position;
            }

            _tmpTargets.Clear();
            return cell.IsValid;
        }

        private void TickTransforming(int now)
        {
            var elapsed = now - _transformStartTick;

            // 전환 중간에 생애 단계(그래픽·그림자)를 2페이즈로 바꾼다. 몸통은 모핑 재질로 그리므로 화면에서는 자연스럽게 이어진다.
            if (!_transformSwapped && elapsed >= Props.transformGraphicSwapTicks)
            {
                _transformSwapped = true;
                SyncLifeStage();
            }

            // 셰이더의 심장 박동(박동 순간)에 맞춰 몸에서 작은 굴절 파동과 핏빛 불씨가 번지고 화면이 울린다.
            var beatTicks = Mathf.RoundToInt(60f / ApocryphaMorphMaterials.BeatRate);
            if (elapsed > 0 && elapsed < Props.transformTicks && elapsed % beatTicks == 0)
            {
                SpawnEffecter(ApocryphaEffecterDefOf.VV_Effecter_ApocryphaMorphPulse, BodyCenterOffset);
                ApocryphaUtility.ShakeCamera(parent.Map, 0.25f + 0.5f * Mathf.Sin(Mathf.PI * TransformProgress));
            }

            if (elapsed >= Props.transformTicks)
            {
                BecomeUnbound();
            }
        }

        private void TickUnbound(int now)
        {
            var stunned = Pawn.stances.stunner.Stunned;
            TickBreath(now, stunned);

            // 인접한 적 폰은 근접 공격한다(돌진 중에도).
            if (!stunned && !IsBreathing && !IsChanneling && now >= _nextMeleeTick)
            {
                _nextMeleeTick = now + Props.meleeIntervalTicks;

                var target = FindAdjacentEnemy();
                if (target != null)
                {
                    Pawn.meleeVerbs.TryMeleeAttack(target);
                }
            }
        }

        private Pawn FindAdjacentEnemy()
        {
            var map = parent.Map;
            var origin = parent.Position;
            foreach (var offset in GenAdj.AdjacentCellsAndInside)
            {
                var cell = origin + offset;
                if (!cell.InBounds(map)) { continue; }

                var things = cell.GetThingList(map);
                for (int i = 0; i < things.Count; i++)
                {
                    if (things[i] is Pawn target && target != parent && !target.Dead && !target.Downed && Pawn.HostileTo(target))
                    {
                        return target;
                    }
                }
            }

            return null;
        }
        #endregion

        #region 독성 브레스
        private void TickBreath(int now, bool stunned)
        {
            if (IsBreathing)
            {
                if (stunned)
                {
                    CancelBreath();
                    _breathReadyTick = now + 300;
                    return;
                }

                Pawn.rotationTracker.FaceCell(_breathCell);
                if (now >= _breathEndTick)
                {
                    ReleaseBreath(now);
                }
                return;
            }

            if (stunned || now < _breathReadyTick || IsChanneling || _pendingSteal) { return; }
            if (!parent.IsHashIntervalTick(BreathTargetScanInterval)) { return; }

            if (TryFindBreathTarget(out var cell))
            {
                _breathCell = cell;
                _breathEndTick = now + Props.breathWarmupTicks;
                Pawn.rotationTracker.FaceCell(cell);
                InterruptCurrentJob();
            }
        }

        // 바라보는 방향 앞쪽(반경 7) 시야 안에서 가장 가까운 적 폰을 노린다.
        private bool TryFindBreathTarget(out IntVec3 cell)
        {
            cell = IntVec3.Invalid;

            var map = parent.Map;
            var origin = parent.Position;
            var facing = Pawn.Rotation.AsAngle;
            var rangeSquared = Props.breathRange * Props.breathRange;
            var bestDistance = float.MaxValue;

            var pawns = map.mapPawns.AllPawnsSpawned;
            for (int i = 0; i < pawns.Count; i++)
            {
                var target = pawns[i];
                if (target == parent || target.Dead || target.Downed) { continue; }

                var distance = (target.Position - origin).LengthHorizontalSquared;
                if (distance > rangeSquared || distance >= bestDistance || distance == 0) { continue; }

                var angle = (target.Position - origin).ToVector3().AngleFlat();
                if (Mathf.Abs(Mathf.DeltaAngle(facing, angle)) > BreathFrontSearchAngle) { continue; }
                if (!Pawn.HostileTo(target)) { continue; }
                if (!GenSight.LineOfSight(origin, target.Position, map, skipFirstCell: true)) { continue; }

                bestDistance = distance;
                cell = target.Position;
            }

            return cell.IsValid;
        }

        private void CancelBreath()
        {
            _breathEndTick = -1;
            _breathCell = IntVec3.Invalid;
        }

        private void ReleaseBreath(int now)
        {
            var map = parent.Map;
            var origin = parent.Position;
            var target = _breathCell;

            CancelBreath();
            _breathReadyTick = now + Props.breathCooldownTicks;
            if (!target.IsValid || target == origin) { return; }

            var baseAngle = (target - origin).ToVector3().AngleFlat();
            var halfAngle = Props.breathAngle * 0.5f;
            var hornetDef = VVThingDefOf.VV_TitanicHornet;

            // 부채꼴 칸은 직접 계산한다(GenExplosion의 affectedAngle은 ±180° 경계에서 잘리는 버그가 있다).
            foreach (var cell in GenRadial.RadialCellsAround(origin, Props.breathRange, useCenter: false))
            {
                if (!cell.InBounds(map)) { continue; }
                if (Mathf.Abs(Mathf.DeltaAngle(baseAngle, (cell - origin).ToVector3().AngleFlat())) > halfAngle) { continue; }
                if (!GenSight.LineOfSight(origin, cell, map, skipFirstCell: true)) { continue; }

                _tmpCellThings.Clear();
                _tmpCellThings.AddRange(cell.GetThingList(map));
                foreach (var thing in _tmpCellThings)
                {
                    if (!(thing is Pawn victim) || victim == parent || victim.Dead) { continue; }
                    if (victim.def == hornetDef) { continue; }
                    if (victim.Faction != null && victim.Faction == Pawn.Faction) { continue; }

                    var toxic = Props.breathToxicSeverity * Mathf.Max(0f, 1f - victim.GetStatValue(StatDefOf.ToxicResistance));
                    if (toxic > 0f)
                    {
                        HealthUtility.AdjustSeverity(victim, HediffDefOf.ToxicBuildup, toxic);
                    }

                    if (Props.breathAcidBurnDamage > 0f && !victim.Dead && DamageDefOf.AcidBurn != null)
                    {
                        victim.TakeDamage(new DamageInfo(DamageDefOf.AcidBurn, Props.breathAcidBurnDamage, 0f, baseAngle, Pawn));
                    }
                }
            }
            _tmpCellThings.Clear();

            ThrowBreathFlecks(baseAngle, halfAngle);
            SpawnEffecter(ApocryphaEffecterDefOf.VV_Effecter_ApocryphaBreath, MouthOffset());
            ApocryphaUtility.ShakeCamera(map, 0.6f);
        }

        // 녹자색 독가스를 입에서 부채꼴로 뿜는다.
        private void ThrowBreathFlecks(float baseAngle, float halfAngle)
        {
            var map = parent.Map;
            var mouth = Pawn.DrawPos + MouthOffset();
            if (!mouth.ShouldSpawnMotesAt(map)) { return; }

            for (int i = 0; i < 42; i++)
            {
                var fleckDef = Rand.Chance(0.3f) ? ApocryphaFleckDefOf.VV_Fleck_ApocryphaToxicGasViolet : ApocryphaFleckDefOf.VV_Fleck_ApocryphaToxicGas;
                var data = FleckMaker.GetDataStatic(mouth + Gen.RandomHorizontalVector(0.3f), map, fleckDef, Rand.Range(1.1f, 2.1f));
                data.velocityAngle = baseAngle + Rand.Range(-halfAngle, halfAngle);
                data.velocitySpeed = Rand.Range(2.5f, 6.5f);
                data.rotation = Rand.Range(0f, 360f);
                data.rotationRate = Rand.Range(-70f, 70f);
                map.flecks.CreateFleck(data);
            }
        }

        // 바라보는 방향에 따른 입 위치(그래픽 크기 비례).
        public Vector3 MouthOffset()
        {
            var size = CurrentVisualDrawSize;
            var rot = Pawn.Rotation;
            if (rot == Rot4.North) { return new Vector3(0f, 0f, 0.28f * size); }
            if (rot == Rot4.East) { return new Vector3(0.36f * size, 0f, 0.05f * size); }
            if (rot == Rot4.West) { return new Vector3(-0.36f * size, 0f, 0.05f * size); }
            return new Vector3(0f, 0f, 0.1f * size);
        }
        #endregion

        #region 피해 처리
        public override void PostPreApplyDamage(ref DamageInfo dinfo, out bool absorbed)
        {
            base.PostPreApplyDamage(ref dinfo, out absorbed);
            if (!parent.Spawned) { return; }

            // 모든 피해는 여기서 흡수하고 페이즈별로 직접 처리한다(신체 부위/출혈/고통 없음).
            absorbed = true;

            var damageDef = dinfo.Def;
            if (damageDef == null || _pendingDeath) { return; }
            if (_phase == ApocryphaPhase.Transforming || _phase == ApocryphaPhase.Dying) { return; }

            // 아포크리파는 기믹 외의 기절에 면역이다. 기절 피해 중에서는 마나 파동 의식의 기절만 직접 적용한다
            // (흡수하면 바닐라 기절 처리가 건너뛰어지므로). 그 밖의 기절 피해는 흡수만 하고 면역 표시를 띄운다.
            if (damageDef == DamageDefOf.Stun || damageDef == DamageDefOf.NerveStun)
            {
                var stunTicks = damageDef.constantStunDurationTicks ?? Mathf.RoundToInt(dinfo.Amount * StunHandler.StunDurationTicksPerDamage);
                if (stunTicks > 0)
                {
                    if (DamageWorker_ManawaveStun.Applying)
                    {
                        GimmickStun(stunTicks, dinfo.Instigator);
                        InterruptChannel();
                    }
                    else
                    {
                        Notify_StunBlocked();
                    }
                }
                return;
            }

            if (!damageDef.harmsHealth) { return; }

            if (_phase == ApocryphaPhase.Bound)
            {
                ThrowBlockedText();
                return;
            }

            var dealt = dinfo.Amount * (1f - DamageReduction);
            if (dealt <= 0f) { return; }

            _vitality = Mathf.Max(0f, _vitality - dealt);
            _pendingDamageText += dealt;

            if (_vitality <= 0f)
            {
                _pendingDeath = true;
            }
        }

        private void ThrowBlockedText()
        {
            var now = GenTicks.TicksGame;
            if (now - _lastBlockedTextTick < BlockedTextMoteInterval) { return; }

            _lastBlockedTextTick = now;
            var drawPos = BodyCenter;
            MoteMaker.ThrowText(
                new Vector3(drawPos.x + 1f, drawPos.y, drawPos.z + 1f),
                parent.Map,
                LocalizeString_Etc.VV_TextMote_DamageBlockedByFairy.Translate());
        }

        // 기믹에 의한 기절만 StunFor까지 통과시킨다(ApocryphaPatch의 StunFor 접두 패치가 이 플래그를 본다).
        private void GimmickStun(int ticks, Thing instigator)
        {
            ApplyingGimmickStun = true;
            try
            {
                Pawn.stances.stunner.StunFor(ticks, instigator, addBattleLog: false);
            }
            finally
            {
                ApplyingGimmickStun = false;
            }
        }

        // [ApocryphaPatch 호출] 기믹이 아닌 기절이 막혔을 때 "기절 면역" 표시(초당 최대 1회).
        internal void Notify_StunBlocked()
        {
            if (!parent.Spawned) { return; }

            var now = GenTicks.TicksGame;
            if (now - _lastStunImmuneTextTick < BlockedTextMoteInterval) { return; }

            _lastStunImmuneTextTick = now;
            var drawPos = BodyCenter;
            MoteMaker.ThrowText(
                new Vector3(drawPos.x + 1f, drawPos.y, drawPos.z + 1.4f),
                parent.Map,
                LocalizeString_EndingBoss.VV_TextMote_ApocryphaStunImmune.Translate());
        }

        private void FlushDamageText(int now)
        {
            _lastDamageTextTick = now;

            var amount = _pendingDamageText;
            _pendingDamageText = 0f;

            var drawPos = BodyCenter;
            MoteMaker.ThrowText(
                new Vector3(drawPos.x + 1.2f, drawPos.y, drawPos.z + 0.8f),
                parent.Map,
                "-" + amount.ToString(amount < 10f ? "0.#" : "0"),
                DamageTextColor);
        }
        #endregion

        #region 계약 API
        // [B 호출] 2페이즈 조율 의식 완료 시: 장시간 기절 + 피해 감소 완화 중첩 +1.
        public void ApplyResonanceWave(Thing source, int stunTicks)
        {
            if (!parent.Spawned || (!IsActive && _phase != ApocryphaPhase.Transforming)) { return; }

            // 중첩 수·감소율 메시지는 의식 쪽(ResonanceWaveUtility)이 이 호출 직후 띄운다(중복 방지).
            if (_phase == ApocryphaPhase.Unbound)
            {
                _resonanceStacks++;
            }

            if (stunTicks > 0)
            {
                GimmickStun(stunTicks, source);
            }

            InterruptChannel();
            CancelAim();
            CancelBreath();

            SpawnEffecter(ApocryphaEffecterDefOf.VV_Effecter_ApocryphaStaggered, BodyCenterOffset);
            ApocryphaUtility.ShakeCamera(parent.Map, 1.2f);
        }

        // [E 호출] 요정 간섭 비비가 완전히 근접했을 때: 결속 수치 대폭 감소, 채널링 중단, 기절, 호위 말벌 증원.
        public void ReleaseBond(Pawn releaser)
        {
            if (!parent.Spawned || _phase != ApocryphaPhase.Bound) { return; }

            _bondCount = System.Math.Max(0L, _bondCount - Props.bondReleaseAmount);

            GimmickStun(Props.releaseStunTicks, releaser);
            InterruptChannel();
            CancelAim();

            // 결속이 풀릴 때마다 방어 본능이 자극되어 호위 말벌이 외곽에서 증원된다.
            ApocryphaUtility.SpawnHornetEscorts(Pawn, Props.releaseEscortCount.RandomInRange, fromMapEdge: true);

            if (_bondCount <= 0)
            {
                StartTransformation();
            }
            else
            {
                Messages.Message(
                    LocalizeString_EndingBoss.VV_Message_ApocryphaBondReleased.Translate(_bondCount.ToString("N0").Named("COUNT")),
                    Pawn,
                    MessageTypeDefOf.PositiveEvent);
            }
        }

        // [E 호출] 요정 비비 사출 포격 1회마다: 결속 수치 -1.
        public void Notify_FairyShellFired()
        {
            if (_phase != ApocryphaPhase.Bound || _bondCount <= 0) { return; }

            _bondCount--;
            if (_bondCount <= 0 && parent.Spawned)
            {
                StartTransformation();
            }
        }

        public void InterruptChannel()
        {
            _pendingSteal = false;

            if (parent.Spawned && Pawn.CurJobDef == ApocryphaJobDefOf.VV_ApocryphaChannel)
            {
                // 작업 종료 처리(Notify_ChannelJobEnded)에서 진행도와 메시지를 정리한다.
                Pawn.jobs.EndCurrentJob(JobCondition.InterruptForced);
            }

            _channelTicks = 0;
        }

        // DEV
        public void DebugReduceBond(float fractionOfInitial)
        {
            if (_phase != ApocryphaPhase.Bound) { return; }

            var amount = (long)System.Math.Round(Props.initialBondCount * (double)fractionOfInitial);
            _bondCount = System.Math.Max(0L, _bondCount - System.Math.Max(1L, amount));
            if (_bondCount <= 0 && parent.Spawned)
            {
                StartTransformation();
            }
        }

        public void DebugForcePhase2()
        {
            if (!parent.Spawned) { return; }

            if (_phase == ApocryphaPhase.Bound)
            {
                _bondCount = 0;
                StartTransformation();
            }
            else if (_phase == ApocryphaPhase.Transforming)
            {
                // 다음 틱에 전환을 마친다.
                _transformStartTick = GenTicks.TicksGame - Props.transformTicks;
            }
        }

        public void DebugDamageVitality(float fractionOfMax)
        {
            if (_phase != ApocryphaPhase.Unbound || _pendingDeath) { return; }

            _vitality = Mathf.Max(0f, _vitality - MaxVitality * fractionOfMax);
            if (_vitality <= 0f)
            {
                _pendingDeath = true;
            }
        }
        #endregion

        #region 영원꽃 찬탈(채널링)
        // 찬탈 대상: 엔딩 보스전 중인 앵커 영원꽃(같은 맵에 스폰되어 있을 때만). 접촉 판정은 제단 중심(3.9) 또는 영원꽃(1.9) 기준.
        public bool TryGetStealTarget(out Thing target, out float range, out ArcanePlant_Everflower everflower)
        {
            target = null;
            range = 0f;
            everflower = null;
            if (!parent.Spawned || !IsQuestBossFight) { return false; }

            var map = parent.Map;
            var anchor = ViviEndingQuest.Current?.AnchorEverflower;
            if (anchor == null || !anchor.Spawned || anchor.Map != map) { return false; }

            everflower = anchor;

            var altar = everflower.EverflowerComp?.EnshrinedAltar;
            if (altar != null && altar.Spawned && altar.Map == map)
            {
                target = altar;
                range = Props.channelRangeAltar;
            }
            else
            {
                target = everflower;
                range = Props.channelRangeEverflower;
            }

            return true;
        }

        public bool InStealRange()
        {
            if (!TryGetStealTarget(out var target, out var range, out _)) { return false; }

            return _devForceChannel || parent.Position.InHorDistOf(target.Position, range);
        }

        // 제단 발판에 닿았는데 다른 일을 하고 있으면 즉시 채널링으로 넘어가게 한다.
        private void CheckChannelContact()
        {
            if (IsChanneling || IsAiming || IsBreathing || _pendingSteal) { return; }
            if (Pawn.stances.stunner.Stunned) { return; }
            if (!InStealRange()) { return; }

            InterruptCurrentJob();
        }

        // [채널 작업] 매 틱 진행. 완료되면 다음 틱에 찬탈한다.
        public void Notify_ChannelTick()
        {
            if (_pendingSteal || !IsQuestBossFight) { return; }

            _channelTicks++;
            if (_channelTicks >= ChannelDurationTicks)
            {
                _pendingSteal = true;
            }
        }

        // [채널 작업] 작업이 어떤 이유로든 끝났을 때. 찬탈이 확정되지 않았다면 진행도는 0으로 돌아간다.
        public void Notify_ChannelJobEnded()
        {
            if (_pendingSteal) { return; }

            var now = GenTicks.TicksGame;
            if (_channelTicks >= ChannelInterruptMessageMinTicks && parent.Spawned && now - _lastChannelInterruptMessageTick > 600)
            {
                _lastChannelInterruptMessageTick = now;
                Messages.Message(LocalizeString_EndingBoss.VV_Message_ApocryphaChannelInterrupted.Translate(), Pawn, MessageTypeDefOf.PositiveEvent);
            }

            _channelTicks = 0;
            _devForceChannel = false;
        }

        // 채널링 완료: 영원꽃을 품은 채 호위 말벌과 함께 사라진다(퀘스트 실패).
        private void StealEverflower()
        {
            if (!parent.Spawned) { return; }

            // 찬탈은 엔딩 보스전 중 앵커 영원꽃이 스폰되어 있을 때만 일어난다(그 사이 엔딩이 끝났으면 취소).
            if (!TryGetStealTarget(out var target, out _, out var everflower))
            {
                _channelTicks = 0;
                _devForceChannel = false;
                return;
            }

            var map = parent.Map;

            _channelTicks = 0;
            _devForceChannel = false;
            CancelAim();
            CancelBreath();

            // 상태 전환(Failed)을 먼저 해서 영원꽃 파괴 통지(Notify_AnchorDestroyed)가 중복 처리되지 않게 한다.
            ViviEndingQuest.Current?.Notify_EverflowerStolen(Pawn);

            var effectCell = (target ?? everflower)?.Position ?? parent.Position;
            ApocryphaEffecterDefOf.VV_Effecter_ApocryphaSteal.Spawn(effectCell, map).Cleanup();
            ApocryphaUtility.ShakeCamera(map, 2f);

            if (everflower != null && !everflower.Destroyed)
            {
                everflower.Destroy(DestroyMode.Vanish);
            }

            foreach (var hornet in ApocryphaUtility.GetEscorts(Pawn))
            {
                if (hornet.Spawned)
                {
                    ApocryphaEffecterDefOf.VV_Effecter_ApocryphaVanish.Spawn(hornet.Position, map).Cleanup();
                }

                if (!hornet.Destroyed)
                {
                    hornet.Destroy(DestroyMode.Vanish);
                }
            }

            _phase = ApocryphaPhase.Dying;
            ApocryphaGimmicks.Notify_BossPhaseChanged(Pawn, ApocryphaPhase.Dying);

            ApocryphaEffecterDefOf.VV_Effecter_ApocryphaVanish.Spawn(parent.Position, map, 2.5f).Cleanup();
            Pawn.Destroy(DestroyMode.Vanish);
        }

        public void DebugStartChannel()
        {
            if (!parent.Spawned || !IsActive || !TryGetStealTarget(out _, out _, out _)) { return; }

            _devForceChannel = true;
            Pawn.stances.stunner.StopStun();
            CancelAim();
            CancelBreath();
            InterruptCurrentJob();
        }

        public void DebugFinishChannel()
        {
            if (!parent.Spawned || !IsActive) { return; }

            if (!IsChanneling)
            {
                DebugStartChannel();
            }

            _channelTicks = Mathf.Max(_channelTicks, ChannelDurationTicks - 1);
        }
        #endregion

        #region 페이즈 전환
        private void StartTransformation()
        {
            if (_phase != ApocryphaPhase.Bound || !parent.Spawned) { return; }

            var map = parent.Map;

            _phase = ApocryphaPhase.Transforming;
            _bondCount = 0;
            _transformStartTick = GenTicks.TicksGame;
            _transformSwapped = false;
            _channelTicks = 0;
            _pendingSteal = false;
            _devForceChannel = false;
            CancelAim();

            SyncPhaseHediffs();

            // 충격파: 맵의 보스 외 모든 생명체가 잠시 기절한다.
            foreach (var pawn in map.mapPawns.AllPawnsSpawned.ToList())
            {
                if (pawn == parent || pawn.Dead || pawn.Downed) { continue; }

                pawn.stances?.stunner?.StunFor(Props.transformStunTicks, Pawn, addBattleLog: false);
            }

            SpawnEffecter(ApocryphaEffecterDefOf.VV_Effecter_ApocryphaShockwave);
            ApocryphaUtility.ShakeCamera(map, 2f);
            InterruptCurrentJob();
            PrepareMorph();

            // 2페이즈 곡 도입부의 충격음이 충격파와 맞도록 60틱 주기를 기다리지 않고 바로 곡을 바꾼다.
            var music = Find.MusicManagerPlay;
            if (music != null && !music.disabled)
            {
                music.CheckTransitions();
            }

            Messages.Message(LocalizeString_EndingBoss.VV_Message_ApocryphaTransformStarted.Translate(), Pawn, MessageTypeDefOf.ThreatBig);
            Find.TickManager.slower.SignalForceNormalSpeedShort();

            ApocryphaGimmicks.Notify_BossPhaseChanged(Pawn, ApocryphaPhase.Transforming);
        }

        private void BecomeUnbound()
        {
            var now = GenTicks.TicksGame;

            _phase = ApocryphaPhase.Unbound;
            _transformSwapped = true;
            _maxVitality = Props.maxVitality;
            _vitality = _maxVitality;
            _resonanceStacks = 0;
            _breathReadyTick = now + 600;
            _nextMeleeTick = now;

            SyncLifeStage();
            SyncPhaseHediffs();
            InterruptCurrentJob();

            // 모핑을 끝내고 원래 몸통 재질(2페이즈 그래픽)로 돌아가며 섬광을 터뜨린다.
            Pawn.Drawer?.renderer?.SetAllGraphicsDirty();
            SpawnEffecter(ApocryphaEffecterDefOf.VV_Effecter_ApocryphaTransform);
            ApocryphaUtility.ShakeCamera(parent.Map, 1.5f);

            Find.LetterStack.ReceiveLetter(
                LocalizeString_EndingBoss.VV_Letter_ApocryphaUnboundLabel.Translate(),
                LocalizeString_EndingBoss.VV_Letter_ApocryphaUnbound.Translate(),
                LetterDefOf.ThreatBig,
                new LookTargets(Pawn));

            ApocryphaGimmicks.Notify_BossPhaseChanged(Pawn, ApocryphaPhase.Unbound);
        }

        // 생명력이 0이 되면 시체를 남기지 않고 소멸 연출 Thing으로 처리를 넘긴 뒤 본체를 제거한다.
        private void StartDying()
        {
            if (_phase == ApocryphaPhase.Dying || !parent.Spawned) { return; }

            var map = parent.Map;
            BeginDissolve(map, parent.Position);

            Find.TickManager.slower.SignalForceNormalSpeedShort();
            Pawn.Destroy(DestroyMode.Vanish);
        }

        private void BeginDissolve(Map map, IntVec3 cell)
        {
            _phase = ApocryphaPhase.Dying;
            _channelTicks = 0;
            _pendingSteal = false;
            CancelAim();
            CancelBreath();

            if (map != null && cell.InBounds(map))
            {
                var dying = (DyingApocrypha)ThingMaker.MakeThing(ApocryphaThingDefOf.VV_DyingApocrypha);
                dying.InitFrom(Pawn);
                GenSpawn.Spawn(dying, cell, map);
            }

            // 호위 말벌은 일반 말벌 습격으로 전환된다.
            ApocryphaUtility.ReleaseEscorts(Pawn);

            if (parent.Spawned)
            {
                ApocryphaGimmicks.Notify_BossPhaseChanged(Pawn, ApocryphaPhase.Dying);
            }
            else if (map != null)
            {
                // 이미 디스폰된 상태(예상 밖의 죽음)에서는 보스 맵을 알 수 없으므로 맵 단위 정리로 대신한다.
                ApocryphaGimmicks.CleanupAll(map);
            }
        }

        // 개발자 도구로 죽이는 등 예상 밖의 죽음도 같은 소멸 연출로 처리한다(종족 hasCorpse=false라 시체는 남지 않는다).
        public override void Notify_Killed(Map prevMap, DamageInfo? dinfo = null)
        {
            base.Notify_Killed(prevMap, dinfo);
            if (_phase == ApocryphaPhase.Dying) { return; }

            BeginDissolve(prevMap, parent.Position);
        }
        #endregion

        #region 그리기/UI
        // 렌더 서브워커(병렬 실행)에서도 읽으므로 저장 필드·Def·현재 틱만 읽는다.
        public float CurrentVisualDrawSize
        {
            get
            {
                var stages = Pawn.kindDef?.lifeStages;
                if (stages.NullOrEmpty()) { return 1f; }

                var boundSize = stages[0].bodyGraphicData?.drawSize.x ?? 1f;
                var unboundSize = stages[stages.Count - 1].bodyGraphicData?.drawSize.x ?? boundSize;
                switch (_phase)
                {
                    case ApocryphaPhase.Bound:
                        return boundSize;

                    case ApocryphaPhase.Transforming:
                        var t = Mathf.Clamp01((GenTicks.TicksGame - _transformStartTick) / (float)Mathf.Max(1, Props.transformTicks));
                        return Mathf.Lerp(boundSize, unboundSize, t);

                    default:
                        return unboundSize;
                }
            }
        }

        // Pawn.DrawAt이 매 프레임 호출한다. 렌더 서브워커에서는 그리지 않는다(병렬).
        public override void PostDraw()
        {
            base.PostDraw();
            if (!parent.Spawned) { return; }

            ApocryphaGimmicks.DrawLinks(Pawn);

            if (_phase == ApocryphaPhase.Bound)
            {
                ApocryphaShroudGlow.Draw(Pawn);
            }

            if (IsAiming && _aimCell.IsValid)
            {
                ApocryphaGimmicks.DrawSoulLink(BodyCenter, _aimCell.ToVector3Shifted(), 0.35f, AimBeamColor);
            }

            if (IsChanneling && TryGetStealTarget(out var target, out _, out var everflower))
            {
                var flowerPos = everflower.DrawPos + everflower.EverflowerComp.RitualEffecterOffset;
                ApocryphaGimmicks.DrawSoulLink(flowerPos, BodyCenter, 0.9f, ChannelBeamColor);
            }
        }

        public override string CompInspectStringExtra()
        {
            var sb = new StringBuilder();

            sb.Append(LocalizeString_EndingBoss.VV_Inspector_ApocryphaPhase.Translate(PhaseLabel(_phase)));

            if (_phase == ApocryphaPhase.Bound)
            {
                sb.AppendLine();
                sb.Append(LocalizeString_EndingBoss.VV_Inspector_ApocryphaBond.Translate(_bondCount.ToString("N0")));
            }
            else if (_phase == ApocryphaPhase.Unbound)
            {
                sb.AppendLine();
                sb.Append(LocalizeString_EndingBoss.VV_Inspector_ApocryphaVitality.Translate(
                    Mathf.CeilToInt(_vitality).ToString("N0"),
                    Mathf.CeilToInt(MaxVitality).ToString("N0")));
                sb.AppendLine();
                sb.Append(LocalizeString_EndingBoss.VV_Inspector_ApocryphaDamageReduction.Translate(
                    DamageReduction.ToStringPercent(),
                    _resonanceStacks));
            }

            if (IsChanneling)
            {
                sb.AppendLine();
                sb.Append(LocalizeString_EndingBoss.VV_Inspector_ApocryphaChanneling.Translate(
                    ChannelProgress.ToStringPercent(),
                    ChannelTicksLeft.ToStringTicksToPeriod()));
            }

            return sb.ToString();
        }

        public static string PhaseLabel(ApocryphaPhase phase)
        {
            switch (phase)
            {
                case ApocryphaPhase.Transforming:
                    return LocalizeString_EndingBoss.VV_Inspector_ApocryphaPhase_Transforming.Translate();

                case ApocryphaPhase.Unbound:
                    return LocalizeString_EndingBoss.VV_Inspector_ApocryphaPhase_Unbound.Translate();

                case ApocryphaPhase.Dying:
                    return LocalizeString_EndingBoss.VV_Inspector_ApocryphaPhase_Dying.Translate();

                default:
                    return LocalizeString_EndingBoss.VV_Inspector_ApocryphaPhase_Bound.Translate();
            }
        }

        public override IEnumerable<Gizmo> CompGetGizmosExtra()
        {
            foreach (var gizmo in base.CompGetGizmosExtra())
            {
                yield return gizmo;
            }

            if (!DebugSettings.godMode) { yield break; }

            var command_reduceBond = new Command_Action();
            command_reduceBond.defaultLabel = "DEV: Bond -25%";
            command_reduceBond.action = () => DebugReduceBond(0.25f);
            yield return command_reduceBond;

            var command_forcePhase2 = new Command_Action();
            command_forcePhase2.defaultLabel = "DEV: Force phase 2";
            command_forcePhase2.action = () => DebugForcePhase2();
            yield return command_forcePhase2;

            var command_damageVitality = new Command_Action();
            command_damageVitality.defaultLabel = "DEV: Vitality -25%";
            command_damageVitality.action = () => DebugDamageVitality(0.25f);
            yield return command_damageVitality;

            var command_startChannel = new Command_Action();
            command_startChannel.defaultLabel = "DEV: Start channel";
            command_startChannel.action = () => DebugStartChannel();
            yield return command_startChannel;

            var command_finishChannel = new Command_Action();
            command_finishChannel.defaultLabel = "DEV: Finish channel";
            command_finishChannel.action = () => DebugFinishChannel();
            yield return command_finishChannel;

            var command_resonance = new Command_Action();
            command_resonance.defaultLabel = "DEV: Resonance wave";
            command_resonance.action = () => ApplyResonanceWave(null, 1200);
            yield return command_resonance;

            var command_fireShell = new Command_Action();
            command_fireShell.defaultLabel = "DEV: Shell ready now";
            command_fireShell.action = () => _shellReadyTick = 0;
            yield return command_fireShell;

            var command_doomSeed = new Command_Action();
            command_doomSeed.defaultLabel = "DEV: Doom seed now";
            command_doomSeed.action = () => _nextDoomSeedTick = 0;
            yield return command_doomSeed;

            var command_fairySeed = new Command_Action();
            command_fairySeed.defaultLabel = "DEV: Fairy seed now";
            command_fairySeed.action = () =>
            {
                _doomSeedsLaunched = Mathf.Max(_doomSeedsLaunched, Props.doomSeedsBeforeFairySeed);
                _nextFairySeedTick = 0;
            };
            yield return command_fairySeed;

            var command_breath = new Command_Action();
            command_breath.defaultLabel = "DEV: Breath ready now";
            command_breath.action = () => _breathReadyTick = 0;
            yield return command_breath;

            var command_approach = new Command_Action();
            command_approach.defaultLabel = "DEV: Approach altar now";
            command_approach.action = () =>
            {
                _nextApproachTick = 0;
                InterruptCurrentJob();
            };
            yield return command_approach;

            var command_escorts = new Command_Action();
            command_escorts.defaultLabel = "DEV: Reinforce escorts";
            command_escorts.action = () => ApocryphaUtility.SpawnHornetEscorts(Pawn, Props.escortReinforceCount.RandomInRange, fromMapEdge: true);
            yield return command_escorts;
        }
        #endregion

        #region 내부 도우미
        public void Notify_Approached()
        {
            _nextApproachTick = GenTicks.TicksGame + Props.approachIntervalTicks.RandomInRange;
        }

        // 다음 작업을 생각 트리가 즉시 다시 고르게 한다.
        private void InterruptCurrentJob()
        {
            if (parent.Spawned && Pawn.jobs?.curJob != null)
            {
                Pawn.jobs.EndCurrentJob(JobCondition.InterruptForced);
            }
        }

        // 보스 몸통 위치(offset)에서 이펙터를 한 번 재생한다.
        private void SpawnEffecter(EffecterDef effecterDef, Vector3 offset = default)
        {
            if (effecterDef == null || !parent.Spawned) { return; }

            effecterDef.Spawn(parent, parent.Map, (Pawn.DrawPos - parent.Position.ToVector3Shifted()).WithY(0f) + offset).Cleanup();
        }

        private int CountOnMap(ThingDef def)
        {
            if (def == null || !parent.Spawned) { return 0; }

            return parent.Map.listerThings.ThingsOfDef(def).Count;
        }
        #endregion
    }
}
