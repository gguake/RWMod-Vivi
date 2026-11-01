using RimWorld;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using Verse;
using Verse.AI;

namespace VVRace
{
    // 세이브에 기록되는 전투 단계 값이므로 기존 값을 변경하면 안 된다.
    public enum ApocryphaPhase : byte
    {
        Bound = 1,
        Transforming = 2,
        Unbound = 3,
        Dying = 4,
    }

    public class CompProperties_Apocrypha : CompProperties
    {
        public long initialBondCount = 1440000000L;
        public long bondReleaseAmount = 360000000L;
        public int releaseStunTicks = 300;
        public IntRange releaseEscortCount = new IntRange(3, 5);

        public float shellRange = 20f;
        // 사출탄 폭발 반경 2.4칸에 보스가 휘말리지 않도록 최소 사거리 안의 대상은 조준과 발사에서 제외한다.
        public float shellMinRange = 2.9f;
        public int shellAimTicks = 240;
        public IntRange shellCastIntervalTicks = new IntRange(180, 300);
        public int maxShellAims = 3;

        public int escortFocusTicks = 600;

        public int rangedAttackerMemoryTicks = 1200;

        public int firstDoomSeedDelayTicks = 1500;
        public IntRange doomSeedIntervalTicks = new IntRange(18000, 27000);
        public int doomSeedVolleyStart = 3;
        public int doomSeedVolleyGrowthEvery = 2;
        public int doomSeedVolleyMax = 10;
        public int maxDoomFlowers = 30;
        public int fairySeedEveryVolleys = 3;
        public int maxFairyFlowers = 2;

        // 보호장 1회는 바닐라 초중량 보스 1기 분량으로 잡아, 4~5회 파괴가 1페이즈 전투량이 되게 한다.
        public float manaShieldMax = 1500f;
        public int shieldBreakStunTicks = 1500;

        public int needleCount = 3;
        public int needleFirstDelayTicks = 900;
        public IntRange needleCastIntervalTicks = new IntRange(480, 720);
        public IntRange needleVolleyCount = new IntRange(1, 3);
        public float needleFanRadius = 5f;
        public float needleFanHalfAngle2 = 50f;
        public float needleFanHalfAngle3 = 65f;
        public float needleFanMinTargetDistance = 9f;
        public int needleAimTicks = 150;
        public float needleTargetRange = 24f;
        public float needleRange = 32f;
        public float needleHalfWidth = 1.5f;
        public float needleDamage = 80f;
        public float needleArmorPenetration = 0.8f;
        // 길이 막혀 직선으로 돌파할 때 쏜 바늘은 길을 막는 벽·문·자연석에 이 배율만큼 큰 피해를 준다(피해 정의의 건물 배율 2와 곱해져 800).
        public float needleBreachDamageFactor = 5f;

        public IntRange approachIntervalTicks = new IntRange(9000, 13500);
        public float approachDistance = 9f;
        public float wanderRadius = 10f;
        public IntRange wanderWaitTicks = new IntRange(600, 1200);

        public IntRange escortReinforceCount = new IntRange(2, 4);
        public float escortGuardRadius = 10f;
        public float escortRetaliationRadius = 25f;

        public int edgeHornetIntervalTicks = 1250;
        public IntRange edgeHornetGroupSize = new IntRange(1, 2);
        public SimpleCurve edgeHornetMaxPointsByThreatPoints = new SimpleCurve
        {
            new CurvePoint(500f, 1125f),
            new CurvePoint(2500f, 2250f),
            new CurvePoint(5000f, 3000f),
            new CurvePoint(10000f, 4500f),
        };
        public float flowerGuardRadius = 6f;
        public float flowerGuardRetaliationRadius = 20f;

        public int transformTicks = 420;
        public int transformGraphicSwapTicks = 210;
        public int transformStunTicks = 420;

        public float maxVitality = 9000f;
        public List<float> damageFactorByStuckNeedles = new List<float> { 1f, 1.5f, 2f, 4f };

        public int unboundNeedleCount = 3;
        public FloatRange unboundNeedlePlantRadius = new FloatRange(5f, 10f);
        public int unboundNeedleActivateTicks = 60;
        public int unboundNeedleLockOnTicks = 150;
        public int unboundNeedleLockedTicks = 75;
        public int allNeedlesStuckStunTicks = 3600;

        public int resonanceCooldownTicks = 15000;

        public int breathCooldownTicks = 1200;
        public int breathWarmupTicks = 90;
        public float breathRange = 7f;
        public float breathAngle = 50f;
        public float breathToxicSeverity = 0.25f;
        public float breathAcidBurnDamage = 8f;
        public int meleeIntervalTicks = 60;
        public int smashIntervalTicks = 30;
        public float smashDamage = 400f;

        public SimpleCurve hitPointsFactorByThreatScale = new SimpleCurve
        {
            new CurvePoint(0.1f, 0.4f),
            new CurvePoint(0.3f, 0.55f),
            new CurvePoint(0.6f, 0.75f),
            new CurvePoint(1f, 1f),
            new CurvePoint(1.55f, 1.3f),
            new CurvePoint(2.2f, 1.6f),
        };
        public SimpleCurve doomBloomTimeFactorByThreatScale = new SimpleCurve
        {
            new CurvePoint(0.1f, 2f),
            new CurvePoint(0.3f, 1.6f),
            new CurvePoint(0.6f, 1.25f),
            new CurvePoint(1f, 1f),
            new CurvePoint(1.55f, 0.8f),
            new CurvePoint(2.2f, 0.65f),
        };

        public int channelTicksBound = 5000;
        public int channelTicksUnbound = 3600;
        public float channelRangeAltar = 3.9f;
        public float channelRangeEverflower = 1.9f;

        public CompProperties_Apocrypha()
        {
            compClass = typeof(CompApocrypha);
        }
    }

    // 동물의 CompTickInterval은 15틱 간격이므로 타이머는 매 틱 호출되는 CompTick에서 처리한다.
    public class CompApocrypha : ThingComp, IRoofCollapseAlert
    {
        private const int RoofCollapseEscapeRadius = 15;

        private static readonly Color AimBeamColor = new Color(1f, 0.86f, 0.45f, 0.85f);
        private static readonly Color ChannelBeamColor = new Color(1f, 0.32f, 0.82f, 1f);
        private static readonly Color DamageTextColor = new Color(1f, 0.72f, 0.4f);
        private static readonly Color ShieldDamageTextColor = new Color(0.55f, 0.85f, 1f);

        private const int ChannelContactCheckInterval = 10;
        private const int ShellTargetScanInterval = 30;
        private const int EnemyDetectInterval = 30;
        private const float ShellAimSpreadRadius = 2.5f;
        private const int BreathTargetScanInterval = 20;
        private const int BlockedTextMoteInterval = 60;
        private const int DamageTextInterval = 30;
        private const int SeedRetryTicks = 600;
        private const int ChannelInterruptMessageMinTicks = 120;
        private const float BreathFrontSearchAngle = 45f;
        private const int NeedleSpawnCheckInterval = 60;
        private const int NeedleStunRecoverTicks = 120;
        private const int PursuitInterruptIntervalTicks = 300;
        // 돌파 계획은 작업을 줄 때마다 갱신된다. 갱신이 끊기면(길이 열림·추격 등) 이 시간 뒤 바늘이 평소 대상으로 돌아간다.
        private const int BreachHoldTicks = 600;
        // 막히기 직전 칸에 도착한 뒤에만 돌파 바늘을 쏜다.
        private const float BreachStandRadius = 2f;

        private static readonly List<Thing> _tmpTargets = new List<Thing>();
        private static readonly List<Thing> _tmpCellThings = new List<Thing>();

        private ApocryphaPhase _phase = ApocryphaPhase.Bound;
        private bool _initialized;
        private long _bondCount;
        private float _vitality;
        private float _maxVitality;
        private int _resonanceReadyTick;
        private int _transformStartTick = -1;
        private bool _transformSwapped;
        private int _channelTicks;
        private bool _pendingSteal;
        private bool _pendingDeath;
        private int _shellReadyTick;
        private List<ApocryphaShellAim> _shellAims = new List<ApocryphaShellAim>();
        private int _breathReadyTick;
        private IntVec3 _breathCell = IntVec3.Invalid;
        private int _breathEndTick = -1;
        private int _nextDoomSeedTick = -1;
        private int _doomVolleysLaunched;
        private int _nextApproachTick;
        private int _nextEdgeHornetTick;
        private List<Pawn> _edgeHornets = new List<Pawn>();
        private int _nextMeleeTick;
        private float _manaShield = -1f;
        private bool _shieldBroken;
        private bool _pendingShieldBreak;
        private int _needleReadyTick;
        private List<ApocryphaNeedle> _needles = new List<ApocryphaNeedle>();
        private bool _needleStunned;
        private Thing _rangedAttacker;
        private int _rangedAttackerUntilTick = -1;

        private Effecter _aimSustainer;
        private int _lastBlockedTextTick = -99999;
        private float _pendingDamageText;
        private int _lastDamageTextTick = -99999;
        private int _lastChannelInterruptMessageTick = -99999;
        private int _lastStunImmuneTextTick = -99999;
        private bool _devForceChannel;
        private bool _pendingPursuitCheck;
        private int _lastPursuitInterruptTick = -99999;
        private IntVec3 _breachTarget = IntVec3.Invalid;
        private IntVec3 _breachStandCell = IntVec3.Invalid;
        private int _breachUntilTick = -1;

        // 기믹 기절을 적용하는 동안만 true로 둔다. 일반 기절은 ApocryphaPatch가 StunFor에서 차단한다.
        internal static bool ApplyingGimmickStun { get; private set; }

        public CompProperties_Apocrypha Props => (CompProperties_Apocrypha)props;

        public Pawn Pawn => (Pawn)parent;

        public ApocryphaPhase Phase => _phase;

        public long BondCount => _bondCount;
        public long InitialBondCount => Props.initialBondCount;

        public float ManaShield => Mathf.Max(0f, _manaShield);
        public float MaxManaShield => Props.manaShieldMax * HitPointsDifficultyFactor;

        public static float ThreatScale => Find.Storyteller?.difficulty?.threatScale ?? 1f;
        public float HitPointsDifficultyFactor => Mathf.Max(0.1f, Props.hitPointsFactorByThreatScale?.Evaluate(ThreatScale) ?? 1f);

        // 꽃은 보스를 참조하지 않으므로 아포크리파 정의의 설정을 읽는다.
        public static float DoomBloomTimeFactor
        {
            get
            {
                var props = ApocryphaThingDefOf.VV_Apocrypha?.GetCompProperties<CompProperties_Apocrypha>();
                return Mathf.Max(0.1f, props?.doomBloomTimeFactorByThreatScale?.Evaluate(ThreatScale) ?? 1f);
            }
        }
        public bool ShieldBroken => _shieldBroken || _pendingShieldBreak;

        public IReadOnlyList<ApocryphaNeedle> Needles => _needles;
        public int NeedleCount => _phase == ApocryphaPhase.Unbound ? Props.unboundNeedleCount : Props.needleCount;

        public float Vitality => _vitality;
        public float MaxVitality => _maxVitality > 0f ? _maxVitality : Props.maxVitality * HitPointsDifficultyFactor;

        public int StuckNeedleCount
        {
            get
            {
                var count = 0;
                for (int i = 0; i < _needles.Count; i++)
                {
                    if (_needles[i] != null && !_needles[i].Destroyed && _needles[i].IsStuck) { count++; }
                }

                return count;
            }
        }

        public float IncomingDamageFactor
        {
            get
            {
                if (_phase != ApocryphaPhase.Unbound) { return 0f; }

                var table = Props.damageFactorByStuckNeedles;
                if (table.NullOrEmpty()) { return 1f; }

                return table[Mathf.Clamp(StuckNeedleCount, 0, table.Count - 1)];
            }
        }

        public int ResonanceCooldownTicksLeft => Mathf.Max(0, _resonanceReadyTick - GenTicks.TicksGame);

        public bool IsChanneling => parent.Spawned && Pawn.CurJobDef == ApocryphaJobDefOf.VV_ApocryphaChannel;
        public int ChannelDurationTicks => _phase == ApocryphaPhase.Unbound ? Props.channelTicksUnbound : Props.channelTicksBound;
        public float ChannelProgress => IsChanneling ? Mathf.Clamp01((float)_channelTicks / ChannelDurationTicks) : 0f;
        public int ChannelTicksLeft => Mathf.Max(0, ChannelDurationTicks - _channelTicks);

        public int DoomVolleysLaunched => _doomVolleysLaunched;

        public int DoomSeedVolleySize => Mathf.Min(Props.doomSeedVolleyMax, Props.doomSeedVolleyStart + _doomVolleysLaunched / Mathf.Max(1, Props.doomSeedVolleyGrowthEvery));

        public bool FairySeedDue => Props.fairySeedEveryVolleys > 0 && (_doomVolleysLaunched + 1) % Props.fairySeedEveryVolleys == 0;

        public bool IsActive => _phase == ApocryphaPhase.Bound || _phase == ApocryphaPhase.Unbound;

        // 병렬 렌더 서브워커에서도 조회하므로 모핑 경과 시간(초)과 진행도(0~1)는 저장 필드와 현재 틱으로만 계산한다.
        public float TransformAgeSecs => _transformStartTick < 0 ? 0f : Mathf.Max(0, GenTicks.TicksGame - _transformStartTick) / 60f;
        public float TransformProgress => _transformStartTick < 0 ? 0f : Mathf.Clamp01((GenTicks.TicksGame - _transformStartTick) / (float)Mathf.Max(1, Props.transformTicks));

        public bool IsQuestBossFight
        {
            get
            {
                var quest = ViviEndingQuest.Current;
                return quest != null && quest.Stage == ViviEndingStage.Boss && quest.Boss == Pawn;
            }
        }
        public bool IsAiming => _shellAims.Count > 0;
        public IReadOnlyList<ApocryphaShellAim> ShellAims => _shellAims;
        public bool IsBreathing => _breathEndTick >= 0;
        public IntVec3 BreathCell => _breathCell;
        public int NextApproachTick => _nextApproachTick;

        // 몸통이 칸 중심보다 위에 그려지므로 연결선·조준선도 ApocryphaGimmicks와 같은 몸통 기준점을 사용한다.
        public Vector3 BodyCenter => ApocryphaGimmicks.GetBossBodyPos(Pawn);

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

        // 불러오기 중 스폰은 별도 스레드에서 실행되지만 ContentFinder의 텍스처 조회는 메인 스레드에서만 가능하다.
        // SetAllGraphicsDirty처럼 긴 작업 완료 후 메인 스레드로 미루며 일반 실행 중에는 즉시 처리한다.
        private void PrepareMorph()
        {
            LongEventHandler.ExecuteWhenFinished(() =>
            {
                ApocryphaMorphMaterials.EnsureInitialized(Props.transformTicks / 60f);
                Pawn.Drawer?.renderer?.SetAllGraphicsDirty();
            });
        }

        private void InitializeState()
        {
            var now = GenTicks.TicksGame;

            _initialized = true;
            _phase = ApocryphaPhase.Bound;
            _bondCount = Props.initialBondCount;
            _maxVitality = Props.maxVitality * HitPointsDifficultyFactor;
            _vitality = _maxVitality;
            _manaShield = MaxManaShield;

            _nextDoomSeedTick = now + Props.firstDoomSeedDelayTicks;
            _needleReadyTick = now + Props.needleFirstDelayTicks;
            _nextApproachTick = now + Props.approachIntervalTicks.RandomInRange;
            _nextEdgeHornetTick = now + Props.edgeHornetIntervalTicks;

            _shellReadyTick = now + 600;
            _breathReadyTick = now;
        }

        private void SyncLifeStage()
        {
            var index = (_phase == ApocryphaPhase.Bound || (_phase == ApocryphaPhase.Transforming && !_transformSwapped)) ? 0 : 1;
            var maxIndex = Pawn.RaceProps.lifeStageAges.Count - 1;
            Pawn.ageTracker.LockCurrentLifeStageIndex(Mathf.Clamp(index, 0, maxIndex));
        }

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
            Scribe_Values.Look(ref _resonanceReadyTick, "resonanceReadyTick", 0);
            Scribe_Values.Look(ref _transformStartTick, "transformStartTick", -1);
            Scribe_Values.Look(ref _transformSwapped, "transformSwapped", false);
            Scribe_Values.Look(ref _channelTicks, "channelTicks", 0);
            Scribe_Values.Look(ref _pendingSteal, "pendingSteal", false);
            Scribe_Values.Look(ref _pendingDeath, "pendingDeath", false);
            Scribe_Values.Look(ref _shellReadyTick, "shellReadyTick", 0);
            Scribe_Collections.Look(ref _shellAims, "shellAims", LookMode.Deep);
            Scribe_Values.Look(ref _breathReadyTick, "breathReadyTick", 0);
            Scribe_Values.Look(ref _breathCell, "breathCell", IntVec3.Invalid);
            Scribe_Values.Look(ref _breathEndTick, "breathEndTick", -1);
            Scribe_Values.Look(ref _nextDoomSeedTick, "nextDoomSeedTick", -1);
            Scribe_Values.Look(ref _doomVolleysLaunched, "doomVolleysLaunched", 0);
            Scribe_Values.Look(ref _nextApproachTick, "nextApproachTick", 0);
            Scribe_Values.Look(ref _nextMeleeTick, "nextMeleeTick", 0);
            Scribe_Values.Look(ref _manaShield, "manaShield", -1f);
            Scribe_Values.Look(ref _shieldBroken, "shieldBroken", false);
            Scribe_Values.Look(ref _pendingShieldBreak, "pendingShieldBreak", false);
            Scribe_Values.Look(ref _needleReadyTick, "needleReadyTick", 0);
            Scribe_Values.Look(ref _needleStunned, "needleStunned", false);
            if (Scribe.mode == LoadSaveMode.Saving && (_rangedAttacker == null || _rangedAttacker.Destroyed || !_rangedAttacker.Spawned))
            {
                _rangedAttacker = null;
            }

            Scribe_References.Look(ref _rangedAttacker, "rangedAttacker");
            Scribe_Values.Look(ref _rangedAttackerUntilTick, "rangedAttackerUntilTick", -1);

            if (Scribe.mode == LoadSaveMode.Saving)
            {
                _needles.RemoveAll(v => v == null || v.Destroyed);
            }

            Scribe_Collections.Look(ref _needles, "needles", LookMode.Reference);

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

                if (_shellAims == null)
                {
                    _shellAims = new List<ApocryphaShellAim>();
                }

                _shellAims.RemoveAll(v => v == null || !v.cell.IsValid);

                if (_needles == null)
                {
                    _needles = new List<ApocryphaNeedle>();
                }

                _needles.RemoveAll(v => v == null);

                // 마나 보호장 필드가 없는 구버전 세이브는 보호장이 가득 찬 상태로 시작한다.
                if (_manaShield < 0f)
                {
                    _manaShield = MaxManaShield;
                }
            }
        }

        public override void PostDeSpawn(Map map, DestroyMode mode = DestroyMode.Vanish)
        {
            base.PostDeSpawn(map, mode);
            CleanupAimVisuals();
        }
        #endregion

        #region 틱
        public override void CompTick()
        {
            base.CompTick();
            if (!parent.Spawned) { return; }

            // 파괴 처리는 피해·작업 호출 스택을 벗어난 다음 틱으로 미룬다.
            // 찬탈 완료와 치명타가 겹치면 기절로 찬탈을 취소할 때처럼 처치를 우선한다.
            if (_pendingDeath)
            {
                _pendingDeath = false;
                StartDying();
                return;
            }

            if (_pendingSteal)
            {
                _pendingSteal = false;
                StealEverflower();
                return;
            }

            if (_pendingShieldBreak)
            {
                _pendingShieldBreak = false;
                if (_phase == ApocryphaPhase.Bound)
                {
                    BreakShield();
                }
            }

            var now = GenTicks.TicksGame;
            if (_pendingPursuitCheck)
            {
                _pendingPursuitCheck = false;
                if (_phase == ApocryphaPhase.Bound)
                {
                    InterruptIdleJobForPursuit(now);
                }
            }

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
            var stunned = Pawn.stances.stunner.Stunned;

            if (_shieldBroken && !stunned)
            {
                RestoreShield();
            }

            if (!IsQuestBossFight)
            {
                if (IsAiming)
                {
                    CancelAim();
                }

                DestroyNeedles();
                return;
            }

            if (!stunned)
            {
                TickSeeds(now);
                TickEnemyDetection();
            }

            TickShell(now, stunned);
            TickNeedles(now, stunned);
        }

        private void TickEdgeHornets()
        {
            var map = parent.Map;
            _edgeHornets.RemoveAll(v => v == null || v.Dead || !v.Spawned || v.Map != map);

            // 쓰러진 말벌이 누적돼 증원이 막히지 않도록 증원 상한에서는 제외한다.
            var hornetPower = Mathf.Max(1f, VVPawnKindDefOf.VV_TitanicHornet.combatPower);
            var maxPoints = Props.edgeHornetMaxPointsByThreatPoints.Evaluate(StorytellerUtility.DefaultThreatPointsNow(map));
            var room = Mathf.FloorToInt((maxPoints - _edgeHornets.Count(v => !v.Downed) * hornetPower) / hornetPower);
            if (room <= 0) { return; }

            _edgeHornets.AddRange(ApocryphaUtility.SpawnFlowerGuardHornets(Pawn, Mathf.Min(room, Props.edgeHornetGroupSize.RandomInRange)));
        }

        private void TickSeeds(int now)
        {
            if (_nextDoomSeedTick < 0 || now < _nextDoomSeedTick) { return; }

            ApocryphaGimmicks.TryLaunchSeedVolley(Pawn, DoomSeedVolleySize, FairySeedDue, out var doomLaunched, out var fairyLaunched);
            if (doomLaunched + fairyLaunched > 0)
            {
                _doomVolleysLaunched++;
                _nextDoomSeedTick = now + Props.doomSeedIntervalTicks.RandomInRange;
            }
            else
            {
                _nextDoomSeedTick = now + SeedRetryTicks;
            }
        }

        // 가장 가까운 적이 바뀔 때마다 호위가 오가지 않도록 현재 적이 계속 보이면 지정 시간만 연장한다.
        private void TickEnemyDetection()
        {
            if (!parent.IsHashIntervalTick(EnemyDetectInterval)) { return; }
            if (!(ApocryphaUtility.FindEscortLord(Pawn)?.LordJob is LordJob_ApocryphaEscort escortJob)) { return; }

            var target = escortJob.FocusTarget;
            if (target == null || !IsShellTarget(target, Props.shellRange) || (!(target is Pawn) && AnyShellTargetPawn(Props.shellRange)))
            {
                target = FindNearestShellTarget();
            }

            if (target != null)
            {
                escortJob.SetFocus(target, GenTicks.TicksGame + Props.escortFocusTicks);
            }
        }

        private void TickShell(int now, bool stunned)
        {
            if (stunned)
            {
                if (IsAiming)
                {
                    CancelAim();
                    _shellReadyTick = now + 120;
                }
                return;
            }

            for (int i = _shellAims.Count - 1; i >= 0; i--)
            {
                var aim = _shellAims[i];
                if (now < aim.fireTick) { continue; }

                _shellAims.RemoveAt(i);
                aim.CleanupVisuals();

                if (!InShellMinRange(aim.cell))
                {
                    FireShell(aim.cell);
                }
            }

            if (_shellAims.Count < Props.maxShellAims && now >= _shellReadyTick && !IsChanneling && !_pendingSteal)
            {
                if (TryFindShellTarget(out var cell))
                {
                    _shellAims.Add(new ApocryphaShellAim(cell, now + Props.shellAimTicks));
                    _shellReadyTick = now + Props.shellCastIntervalTicks.RandomInRange;
                    Pawn.rotationTracker.FaceCell(cell);
                }
                else
                {
                    _shellReadyTick = now + ShellTargetScanInterval;
                }
            }

            TickAimVisuals();
        }

        private void FireShell(IntVec3 cell)
        {
            if (cell.IsValid && cell.InBounds(parent.Map))
            {
                ApocryphaGimmicks.TryFireFairyShell(Pawn, cell);
            }
        }

        private void CancelAim()
        {
            CleanupAimVisuals();
            _shellAims.Clear();
        }

        private void CleanupAimVisuals()
        {
            for (int i = 0; i < _shellAims.Count; i++)
            {
                _shellAims[i].CleanupVisuals();
            }

            _aimSustainer?.Cleanup();
            _aimSustainer = null;
        }

        // 모트·이펙터는 저장되지 않으므로 불러온 후 첫 틱에 다시 생성한다.
        private void TickAimVisuals()
        {
            if (_shellAims.Count == 0)
            {
                _aimSustainer?.Cleanup();
                _aimSustainer = null;
                return;
            }

            var map = parent.Map;
            var bossTarget = new TargetInfo(parent);
            var bodyCenter = BodyCenter;
            for (int i = 0; i < _shellAims.Count; i++)
            {
                _shellAims[i].TickVisuals(Pawn, map, bodyCenter, Props.shellAimTicks);
            }

            if (_aimSustainer == null)
            {
                _aimSustainer = ApocryphaEffecterDefOf.VV_Effecter_ApocryphaAimSustain.Spawn();
                _aimSustainer.Trigger(bossTarget, bossTarget);
            }

            _aimSustainer.EffectTick(bossTarget, bossTarget);
        }

        // 포격이 한곳에 몰리지 않도록 기존 조준 근처의 적은 다른 후보가 없을 때만 선택한다.
        private bool TryFindShellTarget(out IntVec3 cell)
        {
            cell = IntVec3.Invalid;

            CollectShellTargets(_tmpTargets, Props.shellRange);
            _tmpTargets.RemoveAll(v => InShellMinRange(v.Position));
            if (_tmpTargets.Count > 0)
            {
                var attacker = RangedAttacker;
                Thing target;
                if (attacker != null && _tmpTargets.Contains(attacker) && !IsNearAimedCell(attacker.Position))
                {
                    target = attacker;
                }
                else if (!_tmpTargets.Where(v => !IsNearAimedCell(v.Position)).TryRandomElement(out target))
                {
                    target = _tmpTargets.RandomElement();
                }

                cell = target.Position;
            }

            _tmpTargets.Clear();
            return cell.IsValid;
        }

        private bool InShellMinRange(IntVec3 cell)
        {
            return (cell - parent.Position).LengthHorizontalSquared < Props.shellMinRange * Props.shellMinRange;
        }

        private void NoteAttacker(Thing instigator)
        {
            if (instigator == null || instigator == parent || !instigator.Spawned || instigator.Map != parent.Map) { return; }
            if (instigator is Pawn pawn)
            {
                if (pawn.Dead || pawn.Downed || pawn.IsPsychologicallyInvisible()) { return; }
            }
            else if (!(instigator is Building_Turret) && !(instigator is ArcanePlant_Turret))
            {
                return;
            }

            if (!IsShellTargetSide(instigator)) { return; }
            if (instigator != _rangedAttacker && IsShellTarget(instigator, Props.shellRange)) { return; }

            _rangedAttacker = instigator;
            _rangedAttackerUntilTick = GenTicks.TicksGame + Props.rangedAttackerMemoryTicks;
            _pendingPursuitCheck = true;
        }

        private void InterruptIdleJobForPursuit(int now)
        {
            if (now - _lastPursuitInterruptTick < PursuitInterruptIntervalTicks || Pawn.stances.stunner.Stunned) { return; }

            var jobDef = Pawn.CurJobDef;
            if (jobDef != ApocryphaJobDefOf.VV_ApocryphaWait && jobDef != ApocryphaJobDefOf.VV_ApocryphaWander && jobDef != ApocryphaJobDefOf.VV_ApocryphaApproach) { return; }
            if (!TryFindPursuitCell(out _)) { return; }

            _lastPursuitInterruptTick = now;
            Pawn.jobs.EndCurrentJob(JobCondition.InterruptForced);
        }

        public Thing RangedAttacker
        {
            get
            {
                var attacker = _rangedAttacker;
                if (attacker == null || _phase != ApocryphaPhase.Bound || GenTicks.TicksGame > _rangedAttackerUntilTick) { return null; }
                if (attacker.Destroyed || !attacker.Spawned || attacker.Map != parent.Map) { return null; }
                if (attacker is Pawn pawn && (pawn.Dead || pawn.Downed)) { return null; }
                if (!IsShellTargetSide(attacker)) { return null; }

                return attacker;
            }
        }

        public bool TryFindPursuitCell(out IntVec3 cell)
        {
            cell = IntVec3.Invalid;
            var attacker = RangedAttacker;
            if (attacker == null || IsShellTarget(attacker, Props.shellRange)) { return false; }

            var map = parent.Map;
            var origin = parent.Position;
            var toBoss = (origin - attacker.Position).ToVector3();
            toBoss.y = 0f;
            toBoss = toBoss.sqrMagnitude > 0.01f ? toBoss.normalized : Vector3.forward;

            bool Reachable(IntVec3 c) => c.InBounds(map) && c != origin && Pawn.CanReach(c, PathEndMode.OnCell, Danger.Deadly);
            bool CanShellFrom(IntVec3 c) => Reachable(c) && (attacker is Pawn p ? IsShellTargetPawn(p, Props.shellRange, c) : IsShellTargetTurret(attacker, Props.shellRange, c));

            var maxDistance = Mathf.Min(Props.shellRange - 3f, origin.DistanceTo(attacker.Position));
            for (var distance = maxDistance; distance >= Props.shellMinRange + 1f; distance -= 2f)
            {
                var point = (attacker.Position.ToVector3Shifted() + toBoss * distance).ToIntVec3();
                cell = CellFinder.StandableCellNear(point, map, 3f, CanShellFrom);
                if (cell.IsValid) { return true; }
            }

            cell = CellFinder.StandableCellNear(attacker.Position, map, 6f, Reachable);
            return cell.IsValid;
        }

        private bool IsNearAimedCell(IntVec3 cell)
        {
            for (int i = 0; i < _shellAims.Count; i++)
            {
                if (_shellAims[i].cell.InHorDistOf(cell, ShellAimSpreadRadius)) { return true; }
            }

            return false;
        }

        private Thing FindNearestShellTarget()
        {
            CollectShellTargets(_tmpTargets, Props.shellRange);

            Thing best = null;
            var bestDistance = int.MaxValue;
            var origin = parent.Position;
            for (int i = 0; i < _tmpTargets.Count; i++)
            {
                var distance = (_tmpTargets[i].Position - origin).LengthHorizontalSquared;
                if (distance < bestDistance)
                {
                    best = _tmpTargets[i];
                    bestDistance = distance;
                }
            }

            _tmpTargets.Clear();
            return best;
        }

        private void CollectShellTargets(List<Thing> outTargets, float range)
        {
            CollectShellTargets(outTargets, range, parent.Position);
        }

        private void CollectShellTargets(List<Thing> outTargets, float range, IntVec3 origin)
        {
            outTargets.Clear();

            var map = parent.Map;
            var pawns = map.mapPawns.AllPawnsSpawned;
            for (int i = 0; i < pawns.Count; i++)
            {
                if (IsShellTargetPawn(pawns[i], range, origin))
                {
                    outTargets.Add(pawns[i]);
                }
            }

            if (outTargets.Count > 0) { return; }

            var attackTargets = map.listerThings.ThingsInGroup(ThingRequestGroup.AttackTarget);
            for (int i = 0; i < attackTargets.Count; i++)
            {
                if (!(attackTargets[i] is Pawn) && IsShellTargetTurret(attackTargets[i], range, origin))
                {
                    outTargets.Add(attackTargets[i]);
                }
            }
        }

        private bool AnyShellTargetPawn(float range)
        {
            var pawns = parent.Map.mapPawns.AllPawnsSpawned;
            for (int i = 0; i < pawns.Count; i++)
            {
                if (IsShellTargetPawn(pawns[i], range, parent.Position)) { return true; }
            }

            return false;
        }

        private bool IsShellTargetSide(Thing target)
        {
            return Pawn.HostileTo(target);
        }

        private bool IsShellTarget(Thing thing, float range)
        {
            if (thing == null || !thing.Spawned || thing.Map != parent.Map) { return false; }

            return thing is Pawn pawn ? IsShellTargetPawn(pawn, range, parent.Position) : IsShellTargetTurret(thing, range, parent.Position);
        }

        private bool IsShellTargetPawn(Pawn target, float range, IntVec3 origin)
        {
            if (target == parent || target.Dead || target.Downed || target.IsPsychologicallyInvisible()) { return false; }
            if ((target.Position - origin).LengthHorizontalSquared > range * range) { return false; }
            if (!IsShellTargetSide(target)) { return false; }

            return GenSight.LineOfSight(origin, target.Position, parent.Map, skipFirstCell: true);
        }

        private bool IsShellTargetTurret(Thing building, float range, IntVec3 origin)
        {
            if (building.Destroyed) { return false; }
            if (!(building is Building_Turret) && !(building is ArcanePlant_Turret)) { return false; }
            if ((building.Position - origin).LengthHorizontalSquared > range * range) { return false; }
            if (!IsShellTargetSide(building)) { return false; }

            return GenSight.LineOfSightToThing(origin, building, parent.Map, skipFirstCell: true);
        }

        private void TickNeedles(int now, bool stunned)
        {
            if (Props.needleCount <= 0) { return; }

            if (parent.IsHashIntervalTick(NeedleSpawnCheckInterval))
            {
                EnsureNeedles();
            }

            if (stunned)
            {
                if (CancelNeedleAims())
                {
                    _needleReadyTick = Mathf.Max(_needleReadyTick, now + NeedleStunRecoverTicks);
                }
                return;
            }

            if (now < _needleReadyTick || IsChanneling || _pendingSteal) { return; }

            var idle = _needles.Where(v => v.Spawned && v.IsIdle).ToList();
            if (idle.Count > 0 && TryGetBreachAim(now, out var breachCell))
            {
                AimNeedleVolley(idle, breachCell, idle.Count, breach: true);
                _needleReadyTick = now + Props.needleCastIntervalTicks.RandomInRange;
                return;
            }

            if (idle.Count == 0 || !TryFindNeedleTarget(out var cell))
            {
                _needleReadyTick = now + ShellTargetScanInterval;
                return;
            }

            idle.Shuffle();
            AimNeedleVolley(idle, cell, Mathf.Clamp(Props.needleVolleyCount.RandomInRange, 1, idle.Count), breach: false);
            _needleReadyTick = now + Props.needleCastIntervalTicks.RandomInRange;
        }

        private void AimNeedleVolley(List<ApocryphaNeedle> idle, IntVec3 cell, int count, bool breach)
        {
            if (!cell.InHorDistOf(parent.Position, Props.needleFanMinTargetDistance))
            {
                for (int i = 0; i < count; i++)
                {
                    idle[i].StartAim(cell, Props.needleAimTicks, NeedleFanAngle(i, count), breach);
                }
            }
            else
            {
                idle[0].StartAim(cell, Props.needleAimTicks, breach: breach);
            }
        }

        // 1페이즈에 찬탈 대상까지 길이 막혀 있으면 대상까지 그은 직선에서 막히기 직전 칸(standCell)을 찾아 돌파를 시작한다.
        // 막힌 원인이 깊은 물·용암 같은 지형이나 부술 수 없는 건물이면 돌파하지 않고 평소처럼 맴돈다.
        public bool TryStartBreach(Thing target, out IntVec3 standCell)
        {
            standCell = IntVec3.Invalid;
            if (_phase != ApocryphaPhase.Bound || target == null || !target.Spawned || target.Map != parent.Map) { return false; }
            if (Pawn.CanReach(target, PathEndMode.Touch, Danger.Deadly)) { return false; }

            var map = parent.Map;
            var origin = parent.Position;
            var targetRect = target.OccupiedRect();
            var blocked = IntVec3.Invalid;
            standCell = origin;
            foreach (var c in GenSight.PointsOnLineOfSight(origin, target.Position))
            {
                if (c == origin) { continue; }
                if (targetRect.Contains(c)) { break; }
                if (!ApocryphaUtility.IsBreachableCell(map, c)) { return false; }

                // 벽·문은 막힌 칸이다. 설 수 있는데 갈 수 없는 칸은 갇힌 구역이다. 나무·모래주머니처럼 지나갈 수만 있는 칸은 건너뛴다.
                var edifice = c.GetEdifice(map);
                if (edifice != null && ApocryphaUtility.BlocksPath(edifice))
                {
                    blocked = c;
                    break;
                }

                if (!c.Standable(map)) { continue; }

                if (!Pawn.CanReach(c, PathEndMode.OnCell, Danger.Deadly))
                {
                    blocked = c;
                    break;
                }

                standCell = c;
            }

            if (!blocked.IsValid) { return false; }

            _breachTarget = target.Position;
            _breachStandCell = standCell;
            _breachUntilTick = GenTicks.TicksGame + BreachHoldTicks;
            return true;
        }

        private bool TryGetBreachAim(int now, out IntVec3 cell)
        {
            cell = _breachTarget;
            if (_phase != ApocryphaPhase.Bound || now > _breachUntilTick || !_breachTarget.IsValid || !_breachStandCell.IsValid) { return false; }

            return parent.Position.InHorDistOf(_breachStandCell, BreachStandRadius);
        }

        private float? NeedleFanAngle(int index, int count)
        {
            if (count <= 1) { return null; }

            var half = count == 2 ? Props.needleFanHalfAngle2 : Props.needleFanHalfAngle3;
            return Mathf.Lerp(-half, half, index / (float)(count - 1));
        }

        private void TickUnboundNeedles(int now, bool stunned)
        {
            if (!IsQuestBossFight)
            {
                DestroyNeedles();
                return;
            }

            if (Props.unboundNeedleCount <= 0 || !parent.IsHashIntervalTick(NeedleSpawnCheckInterval)) { return; }

            EnsureNeedles();

            var idle = _needles.Where(v => v != null && v.Spawned && v.IsIdle).ToList();
            if (idle.Count > 0)
            {
                ScatterNeedles(idle);
            }
        }

        private void ScatterNeedles(List<ApocryphaNeedle> needles)
        {
            var map = parent.Map;
            var center = parent.Position;
            var chosen = new List<IntVec3>();
            for (int i = 0; i < needles.Count; i++)
            {
                var needle = needles[i];
                if (needle == null || !needle.Spawned) { continue; }

                var cell = FindNeedlePlantCell(map, center, chosen);
                chosen.Add(cell);

                var outDir = (cell.ToVector3Shifted() - BodyCenter).Yto0();
                needle.ScatterTo(cell, outDir.sqrMagnitude > 0.01f ? outDir.normalized : Vector3.forward);
            }
        }

        private IntVec3 FindNeedlePlantCell(Map map, IntVec3 center, List<IntVec3> chosen)
        {
            var radius = Props.unboundNeedlePlantRadius;
            bool Valid(IntVec3 c, bool spaced)
            {
                if (!c.InBounds(map) || !c.Standable(map) || c.Fogged(map)) { return false; }

                var distance = c.DistanceTo(center);
                if (distance < radius.min || distance > radius.max) { return false; }

                return !spaced || !chosen.Any(v => v.InHorDistOf(c, 3f));
            }

            if (CellFinder.TryFindRandomCellNear(center, map, Mathf.CeilToInt(radius.max), c => Valid(c, true), out var cell)
                || CellFinder.TryFindRandomCellNear(center, map, Mathf.CeilToInt(radius.max), c => Valid(c, false), out cell))
            {
                return cell;
            }

            cell = CellFinder.StandableCellNear(center, map, radius.max + 5f);
            return cell.IsValid ? cell : center;
        }

        public void Notify_NeedleStuck(ApocryphaNeedle needle)
        {
            if (_phase != ApocryphaPhase.Unbound || !parent.Spawned) { return; }

            var stuck = StuckNeedleCount;
            if (stuck >= Props.unboundNeedleCount && !_needleStunned)
            {
                _needleStunned = true;
                GimmickStun(Props.allNeedlesStuckStunTicks, null);
                InterruptChannel();
                CancelBreath();

                SpawnEffecter(ApocryphaEffecterDefOf.VV_Effecter_ApocryphaStaggered, ApocryphaGimmicks.GetBossBodyOffset(Pawn));
                ApocryphaUtility.ShakeCamera(parent.Map, 1.5f);
                Messages.Message(LocalizeString_EndingBoss.VV_Message_ApocryphaAllNeedlesStuck.Translate(), Pawn, MessageTypeDefOf.PositiveEvent);
            }
            else
            {
                Messages.Message(
                    LocalizeString_EndingBoss.VV_Message_ApocryphaNeedleStuck.Translate(stuck, Props.unboundNeedleCount, IncomingDamageFactor.ToStringPercent()),
                    Pawn,
                    MessageTypeDefOf.PositiveEvent);
            }
        }

        private void PullOutNeedles()
        {
            _needleStunned = false;

            var map = parent.Map;
            var chosen = new List<IntVec3>();
            var pulled = false;
            for (int i = 0; i < _needles.Count; i++)
            {
                var needle = _needles[i];
                if (needle == null || needle.Destroyed || !needle.IsStuck) { continue; }

                var cell = FindNeedlePlantCell(map, parent.Position, chosen);
                chosen.Add(cell);
                if (needle.PullOut(cell))
                {
                    pulled = true;
                }
            }

            if (pulled)
            {
                ApocryphaUtility.ShakeCamera(parent.Map, 0.8f);
                Messages.Message(LocalizeString_EndingBoss.VV_Message_ApocryphaNeedlesPulled.Translate(), Pawn, MessageTypeDefOf.NeutralEvent);
            }
        }

        private bool TryFindNeedleTarget(out IntVec3 cell)
        {
            return TryFindNeedleTarget(parent.Position, out cell);
        }

        // origin은 거리·시야 판정의 기준 칸이다. 1페이즈는 보스, 2페이즈는 바늘 위치를 사용한다.
        private bool TryFindNeedleTarget(IntVec3 origin, out IntVec3 cell)
        {
            cell = IntVec3.Invalid;

            CollectShellTargets(_tmpTargets, Props.needleTargetRange, origin);
            if (_tmpTargets.Count > 0)
            {
                var attacker = RangedAttacker;
                Thing target;
                if (attacker != null && _tmpTargets.Contains(attacker) && !IsInAimedNeedleLane(attacker.Position))
                {
                    target = attacker;
                }
                else if (!_tmpTargets.Where(v => !IsInAimedNeedleLane(v.Position)).TryRandomElement(out target))
                {
                    target = _tmpTargets.RandomElement();
                }

                cell = target.Position;
            }

            _tmpTargets.Clear();
            return cell.IsValid;
        }

        private bool IsInAimedNeedleLane(IntVec3 cell)
        {
            for (int i = 0; i < _needles.Count; i++)
            {
                if (_needles[i].IsAiming && _needles[i].LaneContains(cell.ToVector3Shifted())) { return true; }
            }

            return false;
        }

        private void EnsureNeedles()
        {
            _needles.RemoveAll(v => v == null || v.Destroyed);
            var count = NeedleCount;

            for (int i = _needles.Count - 1; i >= 0; i--)
            {
                if (_needles[i].Slot >= count)
                {
                    _needles[i].Vanish();
                    _needles.RemoveAt(i);
                }
            }

            if (_needles.Count >= count) { return; }

            for (int slot = 0; slot < count; slot++)
            {
                if (_needles.Any(v => v.Slot == slot)) { continue; }

                var needle = (ApocryphaNeedle)ThingMaker.MakeThing(ApocryphaThingDefOf.VV_ApocryphaNeedle);
                needle.Init(Pawn, slot);
                GenSpawn.Spawn(needle, parent.Position, parent.Map);
                _needles.Add(needle);

            }
        }

        private bool CancelNeedleAims()
        {
            var cancelled = false;
            for (int i = 0; i < _needles.Count; i++)
            {
                if (_needles[i] != null && !_needles[i].Destroyed && _needles[i].CancelAim())
                {
                    cancelled = true;
                }
            }

            return cancelled;
        }

        private void DestroyNeedles()
        {
            if (_needles.Count == 0) { return; }

            for (int i = 0; i < _needles.Count; i++)
            {
                if (_needles[i] != null && !_needles[i].Destroyed)
                {
                    _needles[i].Vanish();
                }
            }

            _needles.Clear();
        }

        private void TickTransforming(int now)
        {
            var elapsed = now - _transformStartTick;

            if (!_transformSwapped && elapsed >= Props.transformGraphicSwapTicks)
            {
                _transformSwapped = true;
                SyncLifeStage();
            }

            var beatTicks = Mathf.RoundToInt(60f / ApocryphaMorphMaterials.BeatRate);
            if (elapsed > 0 && elapsed < Props.transformTicks && elapsed % beatTicks == 0)
            {
                SpawnEffecter(ApocryphaEffecterDefOf.VV_Effecter_ApocryphaMorphPulse, ApocryphaGimmicks.GetBossBodyOffset(Pawn));
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

            if (_needleStunned && !stunned)
            {
                PullOutNeedles();
            }

            TickUnboundNeedles(now, stunned);
            TickBreath(now, stunned);

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

            // GenExplosion.affectedAngle은 ±180° 경계에서 잘리는 문제가 있어 부채꼴 칸을 직접 계산한다.
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

            absorbed = true;

            var damageDef = dinfo.Def;
            if (damageDef == null || _pendingDeath) { return; }
            if (_phase == ApocryphaPhase.Transforming || _phase == ApocryphaPhase.Dying) { return; }

            // 피해를 흡수하면 바닐라 기절 처리가 생략되므로 허용된 마나 파동 기절은 직접 적용한다.
            // 그 밖의 기절 피해는 흡수하고 면역 표시만 출력한다.
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
                NoteAttacker(dinfo.Instigator);

                if (ShieldBroken || _manaShield <= 0f)
                {
                    ThrowBlockedText();
                    return;
                }

                var absorbedByShield = Mathf.Min(_manaShield, dinfo.Amount);
                if (absorbedByShield <= 0f) { return; }

                _manaShield -= absorbedByShield;
                _pendingDamageText += absorbedByShield;

                // 피해 처리 중 재진입하지 않도록 기절·발사는 다음 틱으로 미룬다.
                if (_manaShield <= 0f)
                {
                    _manaShield = 0f;
                    _pendingShieldBreak = true;
                }
                return;
            }

            var dealt = dinfo.Amount * IncomingDamageFactor;
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

        private void BreakShield()
        {
            var now = GenTicks.TicksGame;

            _manaShield = 0f;
            _shieldBroken = true;

            CancelAim();
            CancelNeedleAims();
            InterruptChannel();
            GimmickStun(Props.shieldBreakStunTicks, null);

            SpawnEffecter(ApocryphaEffecterDefOf.VV_Effecter_ApocryphaShieldBreak, ApocryphaGimmicks.GetBossBodyOffset(Pawn));
            ApocryphaUtility.ShakeCamera(parent.Map, 1.2f);

            if (IsQuestBossFight)
            {
                ApocryphaGimmicks.TryLaunchSeedVolley(Pawn, DoomSeedVolleySize, true, out var doomLaunched, out var fairyLaunched);
                if (doomLaunched + fairyLaunched > 0)
                {
                    _doomVolleysLaunched++;
                }

                _nextDoomSeedTick = now + Props.doomSeedIntervalTicks.RandomInRange;
            }

            Messages.Message(LocalizeString_EndingBoss.VV_Message_ApocryphaShieldBroken.Translate(), Pawn, MessageTypeDefOf.PositiveEvent);
        }

        private void RestoreShield()
        {
            _shieldBroken = false;
            _manaShield = MaxManaShield;

            SpawnEffecter(ApocryphaEffecterDefOf.VV_Effecter_ApocryphaShieldRestore, ApocryphaGimmicks.GetBossBodyOffset(Pawn));
            Messages.Message(LocalizeString_EndingBoss.VV_Message_ApocryphaShieldRestored.Translate(), Pawn, MessageTypeDefOf.NeutralEvent);
        }

        // ApocryphaPatch의 StunFor 접두 패치가 이 플래그를 검사해 기믹 기절만 허용한다.
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
                _phase == ApocryphaPhase.Bound ? ShieldDamageTextColor : DamageTextColor);
        }
        #endregion

        #region 계약 API
        public void ApplyResonanceWave(Thing source, int stunTicks)
        {
            if (!parent.Spawned || (!IsActive && _phase != ApocryphaPhase.Transforming)) { return; }

            _resonanceReadyTick = GenTicks.TicksGame + Props.resonanceCooldownTicks;

            if (stunTicks > 0)
            {
                GimmickStun(stunTicks, source);
            }

            InterruptChannel();
            CancelAim();
            CancelNeedleAims();
            CancelBreath();

            SpawnEffecter(ApocryphaEffecterDefOf.VV_Effecter_ApocryphaStaggered, ApocryphaGimmicks.GetBossBodyOffset(Pawn));
            ApocryphaUtility.ShakeCamera(parent.Map, 1.2f);
        }

        public void ReleaseBond(Pawn releaser)
        {
            if (!parent.Spawned || _phase != ApocryphaPhase.Bound) { return; }

            _bondCount = System.Math.Max(0L, _bondCount - Props.bondReleaseAmount);

            GimmickStun(Props.releaseStunTicks, releaser);
            InterruptChannel();
            CancelAim();
            CancelNeedleAims();

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
                // 진행도와 메시지 정리는 작업 종료 알림인 Notify_ChannelJobEnded에서 처리한다.
                Pawn.jobs.EndCurrentJob(JobCondition.InterruptForced);
            }

            _channelTicks = 0;
        }

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
                _transformStartTick = GenTicks.TicksGame - Props.transformTicks;
            }
        }

        public void DebugStickNeedle()
        {
            if (_phase != ApocryphaPhase.Unbound) { return; }

            for (int i = 0; i < _needles.Count; i++)
            {
                if (_needles[i] != null && !_needles[i].Destroyed && !_needles[i].IsStuck && !_needles[i].IsFlying)
                {
                    _needles[i].DebugStick();
                    return;
                }
            }
        }

        public void DebugDamageShield(float fractionOfMax)
        {
            if (_phase != ApocryphaPhase.Bound || ShieldBroken) { return; }

            _manaShield = Mathf.Max(0f, _manaShield - MaxManaShield * fractionOfMax);
            if (_manaShield <= 0f)
            {
                _pendingShieldBreak = true;
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

        private void CheckChannelContact()
        {
            if (IsChanneling || IsBreathing || _pendingSteal) { return; }
            if (Pawn.stances.stunner.Stunned) { return; }
            if (!InStealRange()) { return; }

            InterruptCurrentJob();
        }

        public void Notify_ChannelTick()
        {
            if (_pendingSteal || !IsQuestBossFight) { return; }

            _channelTicks++;
            if (_channelTicks >= ChannelDurationTicks)
            {
                _pendingSteal = true;
            }
        }

        // 보스가 사라질 때도 디스폰 직전에 작업 종료 알림이 오므로 Spawned만으로는 중단 메시지를 제어할 수 없다.
        // 살아 있고 퀘스트 보스전이 계속되는 경우에만 표시한다. 소실 처리는 알림 전에 보스를 퀘스트에서 분리한다.
        public void Notify_ChannelJobEnded()
        {
            if (_pendingSteal) { return; }

            var now = GenTicks.TicksGame;
            if (_channelTicks >= ChannelInterruptMessageMinTicks && parent.Spawned && !Pawn.Dead && IsQuestBossFight && now - _lastChannelInterruptMessageTick > 600)
            {
                _lastChannelInterruptMessageTick = now;
                Messages.Message(LocalizeString_EndingBoss.VV_Message_ApocryphaChannelInterrupted.Translate(), Pawn, MessageTypeDefOf.PositiveEvent);
            }

            _channelTicks = 0;
            _devForceChannel = false;
        }

        private void StealEverflower()
        {
            if (!parent.Spawned) { return; }

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
            DestroyNeedles();

            // 이후 꽃 파괴가 앵커 소실로 중복 처리되지 않도록 찬탈 신호로 퀘스트를 먼저 실패시킨다.
            ViviEndingQuest.Current?.Notify_EverflowerStolen();

            var effectCell = (target ?? everflower)?.Position ?? parent.Position;
            ApocryphaEffecterDefOf.VV_Effecter_ApocryphaSteal.Spawn(effectCell, map).Cleanup();
            ApocryphaUtility.ShakeCamera(map, 2f);

            // 실제 영원꽃과 보스는 즉시 제거하고 찬탈 시퀀서가 이후 모습을 그린다.
            // 제거 전의 그래픽과 위치를 보존하도록 시퀀서를 먼저 생성한다.
            var stolen = (DyingApocrypha)ThingMaker.MakeThing(ApocryphaThingDefOf.VV_DyingApocrypha);
            stolen.InitStolen(Pawn, everflower);

            if (everflower != null && !everflower.Destroyed)
            {
                everflower.Destroy(DestroyMode.Vanish);
            }

            _phase = ApocryphaPhase.Dying;
            ApocryphaGimmicks.Notify_BossPhaseChanged(Pawn, ApocryphaPhase.Dying);

            ViviEndingUtility.VanishEscorts(Pawn);
            GenSpawn.Spawn(stolen, parent.Position, map);
            Pawn.Destroy(DestroyMode.Vanish);
        }

        public void DebugStartChannel()
        {
            if (!parent.Spawned || !IsActive || !TryGetStealTarget(out _, out _, out _)) { return; }

            _devForceChannel = true;
            Pawn.stances.stunner.StopStun();
            CancelAim();
            CancelNeedleAims();
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
            _pendingShieldBreak = false;
            _shieldBroken = false;
            CancelAim();
            ApocryphaUtility.ClearEscortFocus(Pawn);

            _needles.RemoveAll(v => v == null || v.Destroyed);
            ScatterNeedles(_needles.ToList());

            SyncPhaseHediffs();

            foreach (var pawn in map.mapPawns.AllPawnsSpawned.ToList())
            {
                if (pawn == parent || pawn.Dead || pawn.Downed) { continue; }

                pawn.stances?.stunner?.StunFor(Props.transformStunTicks, Pawn, addBattleLog: false);
            }

            SpawnEffecter(ApocryphaEffecterDefOf.VV_Effecter_ApocryphaShockwave);
            ApocryphaUtility.ShakeCamera(map, 2f);
            InterruptCurrentJob();
            PrepareMorph();

            // 곡 도입부의 충격음을 전환 충격파에 맞추도록 60틱 주기를 기다리지 않고 즉시 곡을 바꾼다.
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
            _maxVitality = Props.maxVitality * HitPointsDifficultyFactor;
            _vitality = _maxVitality;
            _resonanceReadyTick = 0;
            _needleStunned = false;
            _breathReadyTick = now + 600;
            _nextMeleeTick = now;

            SyncLifeStage();
            SyncPhaseHediffs();
            InterruptCurrentJob();

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
            // 소멸 연출에 사망 직전 그래픽 크기를 보존하도록 페이즈 변경 전에 연출을 생성한다.
            DyingApocrypha dying = null;
            if (map != null && cell.InBounds(map))
            {
                dying = (DyingApocrypha)ThingMaker.MakeThing(ApocryphaThingDefOf.VV_DyingApocrypha);
                dying.InitFrom(Pawn);
            }

            _phase = ApocryphaPhase.Dying;
            _channelTicks = 0;
            _pendingSteal = false;
            CancelAim();
            CancelBreath();
            DestroyNeedles();

            if (dying != null)
            {
                GenSpawn.Spawn(dying, cell, map);
            }

            // 맵 밖에 남은 호위가 대상 없는 듀티로 남지 않도록 습격으로 돌리고, 맵의 거대 말벌은 모두 보스와 함께 소멸시킨다.
            ApocryphaUtility.ReleaseEscorts(Pawn);
            ViviEndingUtility.VanishAllTitanicHornets(map);

            if (parent.Spawned)
            {
                ApocryphaGimmicks.Notify_BossPhaseChanged(Pawn, ApocryphaPhase.Dying);
            }
            else if (map != null)
            {
                // 예상 밖의 죽음으로 이미 디스폰됐으면 보스의 맵을 알 수 없으므로 맵 단위 정리를 사용한다.
                ApocryphaGimmicks.CleanupAll(map);
            }
        }

        public override void Notify_Killed(Map prevMap, DamageInfo? dinfo = null)
        {
            base.Notify_Killed(prevMap, dinfo);
            if (_phase == ApocryphaPhase.Dying) { return; }

            BeginDissolve(prevMap, parent.Position);
        }

        // 산 지붕 붕괴는 피해 흡수와 무관하게 Kill로 압사시켜 보스전을 건너뛸 수 있다.
        // CompNociosphere처럼 붕괴 전에 안전한 칸으로 순간이동한다.
        // 이동할 곳이 없어도 RemoveThing을 반환해 현재 칸의 압사 처리는 건너뛴다.
        public RoofCollapseResponse Notify_OnBeforeRoofCollapse()
        {
            if (_phase == ApocryphaPhase.Dying || !parent.Spawned) { return RoofCollapseResponse.None; }

            var map = parent.Map;
            var from = parent.Position;
            if (TryFindRoofCollapseEscapeCell(map, from, out var cell))
            {
                ApocryphaEffecterDefOf.VV_Effecter_ApocryphaVanish.Spawn(from, map, 2.5f).Cleanup();
                parent.Position = cell;
                Pawn.Notify_Teleported(endCurrentJob: true, resetTweenedPos: true);
                ApocryphaEffecterDefOf.VV_Effecter_ApocryphaArrive.Spawn(cell, map, 1.5f).Cleanup();
                Messages.Message(LocalizeString_EndingBoss.VV_Message_ApocryphaEscapedRoofCollapse.Translate(), Pawn, MessageTypeDefOf.NeutralEvent);
            }

            return RoofCollapseResponse.RemoveThing;
        }

        private bool TryFindRoofCollapseEscapeCell(Map map, IntVec3 from, out IntVec3 result)
        {
            var collapseBuffer = map.roofCollapseBuffer;
            var traverseParms = TraverseParms.For(Pawn, Danger.Deadly, TraverseMode.PassAllDestroyableThings);
            if (CellFinder.TryFindRandomCellNear(from, map, RoofCollapseEscapeRadius, c =>
                c != from
                && c.Standable(map)
                && !c.Fogged(map)
                && !collapseBuffer.IsMarkedToCollapse(c)
                && !(c.GetRoof(map)?.isThickRoof ?? false)
                && map.reachability.CanReach(from, c, PathEndMode.OnCell, traverseParms), out result))
            {
                return true;
            }

            return CellFinder.TryFindRandomCellNear(from, map, RoofCollapseEscapeRadius * 2, c =>
                c != from && c.Standable(map) && !collapseBuffer.IsMarkedToCollapse(c), out result);
        }
        #endregion

        #region 그리기/UI
        // 병렬 렌더 서브워커에서도 조회하므로 저장 필드·def·현재 틱만 읽는다.
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
                        return Mathf.Lerp(boundSize, unboundSize, TransformProgress);

                    default:
                        return unboundSize;
                }
            }
        }

        // Pawn.DrawAt이 매 프레임 호출한다. 병렬 실행되는 렌더 서브워커에서는 그리지 않는다.
        public override void PostDraw()
        {
            base.PostDraw();
            if (!parent.Spawned) { return; }

            ApocryphaGimmicks.DrawLinks(Pawn);

            if (_phase == ApocryphaPhase.Bound)
            {
                ApocryphaShroudGlow.Draw(Pawn);
            }

            for (int i = 0; i < _shellAims.Count; i++)
            {
                ApocryphaGimmicks.DrawSoulLink(BodyCenter, _shellAims[i].cell.ToVector3Shifted(), 0.35f, AimBeamColor);
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
                sb.AppendLine();
                if (ShieldBroken)
                {
                    sb.Append(LocalizeString_EndingBoss.VV_Inspector_ApocryphaShieldBroken.Translate(
                        Pawn.stances.stunner.StunTicksLeft.ToStringTicksToPeriod()));
                }
                else
                {
                    sb.Append(LocalizeString_EndingBoss.VV_Inspector_ApocryphaShield.Translate(
                        Mathf.CeilToInt(ManaShield).ToString("N0"),
                        Mathf.CeilToInt(MaxManaShield).ToString("N0")));
                }
            }
            else if (_phase == ApocryphaPhase.Unbound)
            {
                sb.AppendLine();
                sb.Append(LocalizeString_EndingBoss.VV_Inspector_ApocryphaVitality.Translate(
                    Mathf.CeilToInt(_vitality).ToString("N0"),
                    Mathf.CeilToInt(MaxVitality).ToString("N0")));
                sb.AppendLine();
                sb.Append(LocalizeString_EndingBoss.VV_Inspector_ApocryphaStuckNeedles.Translate(
                    StuckNeedleCount,
                    Props.unboundNeedleCount,
                    IncomingDamageFactor.ToStringPercent()));
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

            yield return DevCommand("DEV: Bond -25%", () => DebugReduceBond(0.25f));
            yield return DevCommand("DEV: Force phase 2", DebugForcePhase2);
            yield return DevCommand("DEV: Vitality -25%", () => DebugDamageVitality(0.25f));
            yield return DevCommand("DEV: Start channel", DebugStartChannel);
            yield return DevCommand("DEV: Finish channel", DebugFinishChannel);
            yield return DevCommand("DEV: Resonance wave", () => ApplyResonanceWave(null, 1200));
            yield return DevCommand("DEV: Stick a needle", DebugStickNeedle);
            yield return DevCommand("DEV: Shell ready now", () => _shellReadyTick = 0);
            yield return DevCommand("DEV: Needle ready now", () => _needleReadyTick = 0);
            yield return DevCommand("DEV: Mana shield -25%", () => DebugDamageShield(0.25f));
            yield return DevCommand("DEV: Doom seed now", () => _nextDoomSeedTick = 0);

            yield return DevCommand("DEV: Fairy seed now", () => ApocryphaGimmicks.TryLaunchFairySeed(Pawn));

            yield return DevCommand("DEV: Flower guard hornets", () => _edgeHornets.AddRange(ApocryphaUtility.SpawnFlowerGuardHornets(Pawn, Props.edgeHornetGroupSize.max)));
            yield return DevCommand("DEV: Breath ready now", () => _breathReadyTick = 0);
            yield return DevCommand("DEV: Approach altar now", () =>
            {
                _nextApproachTick = 0;
                InterruptCurrentJob();
            });
            yield return DevCommand("DEV: Reinforce escorts", () => ApocryphaUtility.SpawnHornetEscorts(Pawn, Props.escortReinforceCount.RandomInRange, fromMapEdge: true));
        }

        private static Command_Action DevCommand(string label, System.Action action)
        {
            return new Command_Action()
            {
                defaultLabel = label,
                action = action,
            };
        }
        #endregion

        #region 내부 도우미
        public void Notify_Approached()
        {
            _nextApproachTick = GenTicks.TicksGame + Props.approachIntervalTicks.RandomInRange;
        }

        private void InterruptCurrentJob()
        {
            if (parent.Spawned && Pawn.jobs?.curJob != null)
            {
                Pawn.jobs.EndCurrentJob(JobCondition.InterruptForced);
            }
        }

        private void SpawnEffecter(EffecterDef effecterDef, Vector3 offset = default)
        {
            if (effecterDef == null || !parent.Spawned) { return; }

            ApocryphaGimmicks.PlayEffect(effecterDef, parent.Position, parent.Map, (Pawn.DrawPos - parent.Position.ToVector3Shifted()).WithY(0f) + offset);
        }
        #endregion
    }
}
