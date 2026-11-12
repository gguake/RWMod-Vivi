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

        public float shellRange = 20f;
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

        public float manaShieldMax = 1500f;
        public int shieldBreakStunTicks = 3000;

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
        public float needleBreachDamageFactor = 5f;

        public IntRange approachIntervalTicks = new IntRange(9000, 13500);
        public float approachDistance = 9f;
        public float wanderRadius = 10f;
        public IntRange wanderWaitTicks = new IntRange(600, 1200);

        public float escortGuardRadius = 10f;
        public float escortRetaliationRadius = 25f;
        public float flowerGuardRadius = 6f;
        public float flowerGuardRetaliationRadius = 20f;

        public int arrivalHornetCount = 9;
        public int interferenceHornetCount = 6;
        public int interferenceHornetGrowth = 2;
        public int volleyEscortCount = 4;
        public int doomFlowerHornetIntervalTicks = 10000;
        public int doomFlowerHornetCount = 1;
        public FloatRange summonRadiusAroundBoss = new FloatRange(3.5f, 7f);
        public SimpleCurve hornetCountFactorByThreatPoints = new SimpleCurve
        {
            new CurvePoint(500f, 0.5f),
            new CurvePoint(1500f, 0.75f),
            new CurvePoint(2500f, 1f),
            new CurvePoint(5000f, 1.5f),
            new CurvePoint(10000f, 2.2f),
        };

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
        public float needleStickVitalityDamage = 0.1f;
        public int needleStickStunTicks = 420;
        public int allNeedlesStuckStunTicks = 3600;

        public int breathCooldownTicks = 1200;
        public int breathWarmupTicks = 90;
        public float breathRange = 7f;
        public float breathAngle = 50f;
        public float breathToxicSeverity = 0.25f;
        public float breathAcidBurnDamage = 8f;
        public float breathGasRadius = 3.9f;
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
        private const int StealTargetSkipCheckInterval = 250;

        private static readonly Color DamageTextColor = new Color(1f, 0.72f, 0.4f);
        private static readonly Color ShieldDamageTextColor = new Color(0.55f, 0.85f, 1f);

        private const int BlockedTextMoteInterval = 60;
        private const int DamageTextInterval = 30;

        private ApocryphaPhase _phase = ApocryphaPhase.Bound;
        private bool _initialized;
        private long _bondCount;
        private float _vitality;
        private float _maxVitality;
        private float _threatScale = 1f;
        private float _threatPoints;
        private int _transformStartTick = -1;
        private bool _transformSwapped;
        private bool _pendingDeath;
        private int _nextApproachTick;
        private int _releaseCount;
        private int _nextMeleeTick;
        private float _manaShield;
        private bool _shieldBroken;
        private bool _pendingShieldBreak;

        private int _nextStealTargetSkipCheckTick;

        private ApocryphaTargeting _targeting;
        private ApocryphaShellAttack _shell;
        private ApocryphaNeedleAttack _needleAttack;
        private ApocryphaBreathAttack _breath;
        private ApocryphaChanneling _channel;
        private ApocryphaSeedVolley _seeds;

        private int _lastBlockedTextTick = -99999;
        private float _pendingDamageText;
        private int _lastDamageTextTick = -99999;
        private int _lastStunImmuneTextTick = -99999;

        public CompProperties_Apocrypha Props => (CompProperties_Apocrypha)props;

        public Pawn Pawn => (Pawn)parent;

        public ApocryphaPhase Phase => _phase;

        public ApocryphaTargeting Targeting => _targeting;
        public ApocryphaShellAttack Shell => _shell;
        public ApocryphaNeedleAttack NeedleAttack => _needleAttack;
        public ApocryphaBreathAttack Breath => _breath;
        public ApocryphaChanneling Channel => _channel;

        public long BondCount => _bondCount;
        public long InitialBondCount => Props.initialBondCount;

        public float ManaShield => _manaShield;
        public float MaxManaShield => Props.manaShieldMax * HitPointsDifficultyFactor;

        public float HitPointsDifficultyFactor => Mathf.Max(0.1f, Props.hitPointsFactorByThreatScale?.Evaluate(_threatScale) ?? 1f);
        public float DoomBloomTimeFactor => Mathf.Max(0.1f, Props.doomBloomTimeFactorByThreatScale?.Evaluate(_threatScale) ?? 1f);

        public int ArrivalHornetCount => ScaledHornetCount(Props.arrivalHornetCount);

        public int InterferenceHornetCount(int releaseIndex)
        {
            return ScaledHornetCount(Props.interferenceHornetCount + Props.interferenceHornetGrowth * releaseIndex);
        }

        private int ScaledHornetCount(int baseCount)
        {
            var factor = Props.hornetCountFactorByThreatPoints?.Evaluate(_threatPoints) ?? 1f;
            return Mathf.Max(1, Mathf.RoundToInt(baseCount * factor));
        }
        public bool ShieldBroken => _shieldBroken || _pendingShieldBreak;

        public float Vitality => _vitality;
        public float MaxVitality => _maxVitality;

        public float IncomingDamageFactor
        {
            get
            {
                if (_phase != ApocryphaPhase.Unbound) { return 0f; }

                var table = Props.damageFactorByStuckNeedles;
                if (table.NullOrEmpty()) { return 1f; }

                return table[Mathf.Clamp(_needleAttack.StuckNeedleCount, 0, table.Count - 1)];
            }
        }

        public bool IsActive => _phase == ApocryphaPhase.Bound || _phase == ApocryphaPhase.Unbound;

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
        public int NextApproachTick => _nextApproachTick;

        public Vector3 BodyCenter => ApocryphaGimmicks.GetBossBodyPos(Pawn);

        public CompApocrypha()
        {
            _targeting = new ApocryphaTargeting(this);
            _shell = new ApocryphaShellAttack(this);
            _needleAttack = new ApocryphaNeedleAttack(this);
            _breath = new ApocryphaBreathAttack(this);
            _channel = new ApocryphaChanneling(this);
            _seeds = new ApocryphaSeedVolley(this);
        }

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
            _threatScale = Find.Storyteller?.difficulty?.threatScale ?? 1f;
            _threatPoints = parent.MapHeld != null ? StorytellerUtility.DefaultThreatPointsNow(parent.MapHeld) : 0f;
            _bondCount = Props.initialBondCount;
            _maxVitality = Props.maxVitality * HitPointsDifficultyFactor;
            _vitality = _maxVitality;
            _manaShield = MaxManaShield;

            _nextApproachTick = now + Props.approachIntervalTicks.RandomInRange;

            _seeds.SetReadyTick(now + Props.firstDoomSeedDelayTicks);
            _needleAttack.SetReadyTick(now + Props.needleFirstDelayTicks);
            _shell.SetReadyTick(now + 600);
            _breath.SetReadyTick(now);
        }

        private void SyncLifeStage()
        {
            var index = (_phase == ApocryphaPhase.Bound || (_phase == ApocryphaPhase.Transforming && !_transformSwapped)) ? 0 : 1;
            var maxIndex = Pawn.RaceProps.lifeStageAges.Count - 1;
            Pawn.ageTracker.LockCurrentLifeStageIndex(Mathf.Clamp(index, 0, maxIndex));
        }

        private void SyncPhaseHediffs()
        {
            var def = VVHediffDefOf.VV_ApocryphaFairyBond;
            var shouldHave = _phase == ApocryphaPhase.Bound;
            var hediff = Pawn.health.hediffSet.GetFirstHediffOfDef(def);
            if (shouldHave && hediff == null)
            {
                Pawn.health.AddHediff(def);
            }
            else if (!shouldHave && hediff != null)
            {
                Pawn.health.RemoveHediff(hediff);
            }

            _needleAttack.SyncStuckNeedleHediff();
        }

        public override void PostExposeData()
        {
            base.PostExposeData();

            Scribe_Values.Look(ref _phase, "phase", ApocryphaPhase.Bound);
            Scribe_Values.Look(ref _initialized, "initialized", false);
            Scribe_Values.Look(ref _bondCount, "bondCount", 0L);
            Scribe_Values.Look(ref _vitality, "vitality", 0f);
            Scribe_Values.Look(ref _maxVitality, "maxVitality", 0f);
            Scribe_Values.Look(ref _threatScale, "threatScale", 1f);
            Scribe_Values.Look(ref _threatPoints, "threatPoints", 0f);
            Scribe_Values.Look(ref _transformStartTick, "transformStartTick", -1);
            Scribe_Values.Look(ref _transformSwapped, "transformSwapped", false);
            Scribe_Values.Look(ref _pendingDeath, "pendingDeath", false);
            Scribe_Values.Look(ref _nextApproachTick, "nextApproachTick", 0);
            Scribe_Values.Look(ref _nextMeleeTick, "nextMeleeTick", 0);
            Scribe_Values.Look(ref _manaShield, "manaShield", 0f);
            Scribe_Values.Look(ref _shieldBroken, "shieldBroken", false);
            Scribe_Values.Look(ref _pendingShieldBreak, "pendingShieldBreak", false);
            Scribe_Values.Look(ref _releaseCount, "releaseCount", 0);

            Scribe_Deep.Look(ref _targeting, "targeting", this);
            Scribe_Deep.Look(ref _shell, "shell", this);
            Scribe_Deep.Look(ref _needleAttack, "needleAttack", this);
            Scribe_Deep.Look(ref _breath, "breath", this);
            Scribe_Deep.Look(ref _channel, "channel", this);
            Scribe_Deep.Look(ref _seeds, "seeds", this);
        }

        public override void PostDeSpawn(Map map, DestroyMode mode = DestroyMode.Vanish)
        {
            base.PostDeSpawn(map, mode);
            _shell.CleanupVisuals();
        }
        #endregion

        #region 틱
        public override void CompTick()
        {
            base.CompTick();
            if (!parent.Spawned) { return; }

            if (_pendingDeath)
            {
                _pendingDeath = false;
                StartDying();
                return;
            }

            if (_channel.PendingSteal)
            {
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
            _targeting.Tick(now);

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

            if (IsActive)
            {
                _channel.TickContactCheck();
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
                if (_shell.IsAiming)
                {
                    _shell.Cancel();
                }

                _needleAttack.DestroyAll();
                return;
            }

            if (!stunned)
            {
                _seeds.Tick(now);
                _targeting.TickEnemyDetection();
            }

            _shell.Tick(now, stunned);
            _needleAttack.TickBound(now, stunned);
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
                SpawnEffecter(VVEffecterDefOf.VV_Effecter_ApocryphaMorphPulse, ApocryphaGimmicks.GetBossBodyOffset(Pawn));
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

            _needleAttack.TickUnbound(stunned);
            _breath.Tick(now, stunned);

            if (!stunned && !_breath.IsBreathing && !_channel.IsChanneling && now >= _nextMeleeTick)
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

        #region 피해 처리
        public override void PostPreApplyDamage(ref DamageInfo dinfo, out bool absorbed)
        {
            base.PostPreApplyDamage(ref dinfo, out absorbed);
            if (!parent.Spawned) { return; }

            absorbed = true;

            var damageDef = dinfo.Def;
            if (damageDef == null || _pendingDeath) { return; }
            if (_phase == ApocryphaPhase.Transforming || _phase == ApocryphaPhase.Dying) { return; }

            if (damageDef == DamageDefOf.Stun || damageDef == DamageDefOf.NerveStun)
            {
                var stunTicks = damageDef.constantStunDurationTicks ?? Mathf.RoundToInt(dinfo.Amount * StunHandler.StunDurationTicksPerDamage);
                if (stunTicks > 0)
                {
                    if (DamageWorker_ManawaveStun.Applying)
                    {
                        GimmickStun(stunTicks, dinfo.Instigator);
                        _channel.Interrupt();
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
                _targeting.NoteAttacker(dinfo.Instigator);

                if (ShieldBroken || _manaShield <= 0f)
                {
                    ThrowBlockedText();
                    return;
                }

                var absorbedByShield = Mathf.Min(_manaShield, dinfo.Amount);
                if (absorbedByShield <= 0f) { return; }

                _manaShield -= absorbedByShield;
                _pendingDamageText += absorbedByShield;

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

            _shell.Cancel();
            _needleAttack.CancelAims();
            _channel.Interrupt();
            GimmickStun(Props.shieldBreakStunTicks, null);

            SpawnEffecter(VVEffecterDefOf.VV_Effecter_ApocryphaShieldBreak, ApocryphaGimmicks.GetBossBodyOffset(Pawn));
            ApocryphaUtility.ShakeCamera(parent.Map, 1.2f);

            if (IsQuestBossFight)
            {
                _seeds.Notify_ShieldBroken(now);
            }

            Messages.Message(LocalizeString_Message.VV_Message_ApocryphaShieldBroken.Translate(), Pawn, MessageTypeDefOf.PositiveEvent);
        }

        private void RestoreShield()
        {
            _shieldBroken = false;
            _manaShield = MaxManaShield;

            SpawnEffecter(VVEffecterDefOf.VV_Effecter_ApocryphaShieldRestore, ApocryphaGimmicks.GetBossBodyOffset(Pawn));
            Messages.Message(LocalizeString_Message.VV_Message_ApocryphaShieldRestored.Translate(), Pawn, MessageTypeDefOf.NeutralEvent);
        }

        internal void GimmickStun(int ticks, Thing instigator, bool replaceExisting = false)
        {
            if (replaceExisting)
            {
                Pawn.stances.stunner.StopStun();
            }

            Pawn.stances.stunner.StunFor(ticks, instigator, addBattleLog: false);
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
                LocalizeString_Etc.VV_TextMote_ApocryphaStunImmune.Translate());
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
        public void ReleaseBond(Pawn releaser)
        {
            if (!parent.Spawned || _phase != ApocryphaPhase.Bound) { return; }

            _bondCount = System.Math.Max(0L, _bondCount - Props.bondReleaseAmount);

            GimmickStun(Props.releaseStunTicks, releaser, replaceExisting: true);
            _channel.Interrupt();
            _shell.Cancel();
            _needleAttack.CancelAims();

            var curJob = Pawn.CurJob;
            if (curJob != null && curJob.def == VVJobDefOf.VV_ApocryphaWait && curJob.expiryInterval > Props.releaseStunTicks + 1)
            {
                InterruptCurrentJob();
            }

            if (IsQuestBossFight)
            {
                HornetManifestation.SummonAround(Pawn, InterferenceHornetCount(_releaseCount), HornetSummonRole.Assault);
            }
            _releaseCount++;

            if (_bondCount <= 0)
            {
                StartTransformation();
            }
            else
            {
                Messages.Message(
                    LocalizeString_Message.VV_Message_ApocryphaBondReleased.Translate(_bondCount.ToString("N0").Named("COUNT")),
                    Pawn,
                    MessageTypeDefOf.PositiveEvent);
            }
        }

        internal void TakeVitalityDamage(float amount)
        {
            if (_phase != ApocryphaPhase.Unbound || _pendingDeath || amount <= 0f) { return; }

            _vitality = Mathf.Max(0f, _vitality - amount);
            _pendingDamageText += amount;
            if (_vitality <= 0f)
            {
                _pendingDeath = true;
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

        public void Notify_Approached()
        {
            _nextApproachTick = GenTicks.TicksGame + Props.approachIntervalTicks.RandomInRange;
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

        #region 페이즈 전환
        private void StealEverflower()
        {
            _channel.Reset();
            if (!_channel.TryGetStealTarget(out var target, out _, out var everflower)) { return; }

            var map = parent.Map;

            _shell.Cancel();
            _breath.Cancel();
            _needleAttack.DestroyAll();

            ViviEndingQuest.Current?.Notify_EverflowerStolen();

            var effectCell = (target ?? everflower)?.Position ?? parent.Position;
            VVEffecterDefOf.VV_Effecter_ApocryphaSteal.Spawn(effectCell, map).Cleanup();
            ApocryphaUtility.ShakeCamera(map, 2f);

            var stolen = (DyingApocrypha)ThingMaker.MakeThing(VVThingDefOf.VV_DyingApocrypha);
            stolen.InitStolen(Pawn, everflower);

            if (everflower != null && !everflower.Destroyed)
            {
                everflower.Destroy(DestroyMode.Vanish);
            }

            _phase = ApocryphaPhase.Dying;
            ApocryphaGimmicks.Notify_BossPhaseChanged(Pawn, ApocryphaPhase.Dying);

            ViviEndingUtility.VanishEscorts(Pawn);
            HornetManifestation.CancelAll(map);
            GenSpawn.Spawn(stolen, parent.Position, map);
            Pawn.Destroy(DestroyMode.Vanish);
        }

        private void StartTransformation()
        {
            if (_phase != ApocryphaPhase.Bound || !parent.Spawned) { return; }

            var map = parent.Map;

            _phase = ApocryphaPhase.Transforming;
            _bondCount = 0;
            _transformStartTick = GenTicks.TicksGame;
            _transformSwapped = false;
            _channel.Reset();
            _pendingShieldBreak = false;
            _shieldBroken = false;
            _shell.Cancel();
            ApocryphaUtility.ClearEscortFocus(Pawn);

            _needleAttack.ScatterAll();

            SyncPhaseHediffs();

            foreach (var pawn in map.mapPawns.AllPawnsSpawned.ToList())
            {
                if (pawn == parent || pawn.Dead || pawn.Downed) { continue; }

                pawn.stances?.stunner?.StunFor(Props.transformStunTicks, Pawn, addBattleLog: false);
            }

            SpawnEffecter(VVEffecterDefOf.VV_Effecter_ApocryphaShockwave);
            ApocryphaUtility.ShakeCamera(map, 2f);
            InterruptCurrentJob();
            PrepareMorph();

            var music = Find.MusicManagerPlay;
            if (music != null && !music.disabled)
            {
                music.CheckTransitions();
            }

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
            _needleAttack.ClearStun();
            _breath.SetReadyTick(now + 600);
            _nextMeleeTick = now;

            SyncLifeStage();
            SyncPhaseHediffs();
            InterruptCurrentJob();

            Pawn.Drawer?.renderer?.SetAllGraphicsDirty();
            SpawnEffecter(VVEffecterDefOf.VV_Effecter_ApocryphaTransform);
            ApocryphaUtility.ShakeCamera(parent.Map, 1.5f);

            Find.LetterStack.ReceiveLetter(
                LocalizeString_Letter.VV_Letter_ApocryphaUnboundLabel.Translate(),
                LocalizeString_Letter.VV_Letter_ApocryphaUnbound.Translate(),
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
            DyingApocrypha dying = null;
            if (map != null && cell.InBounds(map))
            {
                dying = (DyingApocrypha)ThingMaker.MakeThing(VVThingDefOf.VV_DyingApocrypha);
                dying.InitFrom(Pawn);
            }

            _phase = ApocryphaPhase.Dying;
            _channel.Reset();
            _shell.Cancel();
            _breath.Cancel();
            _needleAttack.DestroyAll();

            if (dying != null)
            {
                GenSpawn.Spawn(dying, cell, map);
            }

            ApocryphaUtility.ReleaseEscorts(Pawn);
            ViviEndingUtility.VanishAllTitanicHornets(map);
            HornetManifestation.CancelAll(map);

            if (parent.Spawned)
            {
                ApocryphaGimmicks.Notify_BossPhaseChanged(Pawn, ApocryphaPhase.Dying);
            }
            else if (map != null)
            {
                ApocryphaGimmicks.CleanupAll(map);
            }
        }

        public override void Notify_Killed(Map prevMap, DamageInfo? dinfo = null)
        {
            base.Notify_Killed(prevMap, dinfo);
            if (_phase == ApocryphaPhase.Dying) { return; }

            BeginDissolve(prevMap, parent.Position);
        }

        public RoofCollapseResponse Notify_OnBeforeRoofCollapse()
        {
            if (_phase == ApocryphaPhase.Dying || !parent.Spawned) { return RoofCollapseResponse.None; }

            var map = parent.Map;
            var from = parent.Position;
            if (TryFindRoofCollapseEscapeCell(map, from, out var cell))
            {
                TeleportTo(cell, endCurrentJob: true);
                Messages.Message(LocalizeString_Message.VV_Message_ApocryphaEscapedRoofCollapse.Translate(), Pawn, MessageTypeDefOf.NeutralEvent);
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

        private void TeleportTo(IntVec3 cell, bool endCurrentJob)
        {
            var map = parent.Map;
            VVEffecterDefOf.VV_Effecter_ApocryphaVanish.Spawn(parent.Position, map, 2.5f).Cleanup();
            parent.Position = cell;
            Pawn.Notify_Teleported(endCurrentJob, resetTweenedPos: true);
            VVEffecterDefOf.VV_Effecter_ApocryphaArrive.Spawn(cell, map, 1.5f).Cleanup();
        }

        internal bool TrySkipToStealTarget(Thing target, float stealRange)
        {
            if (!parent.Spawned || !IsActive || target == null || !target.Spawned || target.Map != parent.Map) { return false; }

            var now = GenTicks.TicksGame;
            if (now < _nextStealTargetSkipCheckTick) { return false; }

            _nextStealTargetSkipCheckTick = now + StealTargetSkipCheckInterval;

            var map = parent.Map;
            var from = parent.Position;
            if (ApocryphaUtility.CanBreachReach(map, from, target)) { return false; }
            if (!ApocryphaUtility.TryFindSkipCell(map, target, from, stealRange, out var cell)) { return false; }

            _shell.Cancel();
            _needleAttack.CancelAims();
            _needleAttack.EndBreach();
            TeleportTo(cell, endCurrentJob: false);
            return true;
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

            _shell.Draw();
            _channel.Draw();
        }

        public override string CompInspectStringExtra()
        {
            var sb = new StringBuilder();

            sb.Append(LocalizeString_Inspector.VV_Inspector_ApocryphaPhase.Translate(PhaseLabel(_phase)));

            if (_phase == ApocryphaPhase.Bound)
            {
                sb.AppendLine();
                sb.Append(LocalizeString_Inspector.VV_Inspector_ApocryphaBond.Translate(_bondCount.ToString("N0")));
                sb.AppendLine();
                if (ShieldBroken)
                {
                    sb.Append(LocalizeString_Inspector.VV_Inspector_ApocryphaShieldBroken.Translate(
                        Pawn.stances.stunner.StunTicksLeft.ToStringTicksToPeriod()));
                }
                else
                {
                    sb.Append(LocalizeString_Inspector.VV_Inspector_ApocryphaShield.Translate(
                        Mathf.CeilToInt(ManaShield).ToString("N0"),
                        Mathf.CeilToInt(MaxManaShield).ToString("N0")));
                }
            }
            else if (_phase == ApocryphaPhase.Unbound)
            {
                sb.AppendLine();
                sb.Append(LocalizeString_Inspector.VV_Inspector_ApocryphaVitality.Translate(
                    Mathf.CeilToInt(_vitality).ToString("N0"),
                    Mathf.CeilToInt(MaxVitality).ToString("N0")));
            }

            if (_channel.IsChanneling)
            {
                sb.AppendLine();
                sb.Append(LocalizeString_Inspector.VV_Inspector_ApocryphaChanneling.Translate(
                    _channel.Progress.ToStringPercent(),
                    _channel.TicksLeft.ToStringTicksToPeriod()));
            }

            return sb.ToString();
        }

        public static string PhaseLabel(ApocryphaPhase phase)
        {
            switch (phase)
            {
                case ApocryphaPhase.Transforming:
                    return LocalizeString_Inspector.VV_Inspector_ApocryphaPhase_Transforming.Translate();

                case ApocryphaPhase.Unbound:
                    return LocalizeString_Inspector.VV_Inspector_ApocryphaPhase_Unbound.Translate();

                case ApocryphaPhase.Dying:
                    return LocalizeString_Inspector.VV_Inspector_ApocryphaPhase_Dying.Translate();

                default:
                    return LocalizeString_Inspector.VV_Inspector_ApocryphaPhase_Bound.Translate();
            }
        }

        public override IEnumerable<Gizmo> CompGetGizmosExtra()
        {
            foreach (var gizmo in base.CompGetGizmosExtra())
            {
                yield return gizmo;
            }

            if (!DebugSettings.godMode) { yield break; }

            yield return new Command_Action()
            {
                defaultLabel = "DEV: Bond -25%",
                action = () => DebugReduceBond(0.25f),
            };
            yield return new Command_Action()
            {
                defaultLabel = "DEV: Force phase 2",
                action = DebugForcePhase2,
            };
            yield return new Command_Action()
            {
                defaultLabel = "DEV: Vitality -25%",
                action = () => DebugDamageVitality(0.25f),
            };
            yield return new Command_Action()
            {
                defaultLabel = "DEV: Start channel",
                action = _channel.DebugStart,
            };
            yield return new Command_Action()
            {
                defaultLabel = "DEV: Finish channel",
                action = _channel.DebugFinish,
            };
            yield return new Command_Action()
            {
                defaultLabel = "DEV: Stick a needle",
                action = _needleAttack.DebugStickNeedle,
            };
            yield return new Command_Action()
            {
                defaultLabel = "DEV: Shell ready now",
                action = () => _shell.SetReadyTick(0),
            };
            yield return new Command_Action()
            {
                defaultLabel = "DEV: Needle ready now",
                action = () => _needleAttack.SetReadyTick(0),
            };
            yield return new Command_Action()
            {
                defaultLabel = "DEV: Mana shield -25%",
                action = () => DebugDamageShield(0.25f),
            };
            yield return new Command_Action()
            {
                defaultLabel = "DEV: Doom seed now",
                action = () => _seeds.SetReadyTick(0),
            };

            yield return new Command_Action()
            {
                defaultLabel = "DEV: Fairy seed now",
                action = () => ApocryphaGimmicks.TryLaunchFairySeed(Pawn),
            };

            yield return new Command_Action()
            {
                defaultLabel = "DEV: Summon assault hornets",
                action = () => HornetManifestation.SummonAround(Pawn, InterferenceHornetCount(_releaseCount), HornetSummonRole.Assault),
            };
            yield return new Command_Action()
            {
                defaultLabel = "DEV: Breath ready now",
                action = () => _breath.SetReadyTick(0),
            };
            yield return new Command_Action()
            {
                defaultLabel = "DEV: Approach altar now",
                action = () =>
                {
                    _nextApproachTick = 0;
                    _nextStealTargetSkipCheckTick = 0;
                    InterruptCurrentJob();
                },
            };
            yield return new Command_Action()
            {
                defaultLabel = "DEV: Summon escort hornets",
                action = () => HornetManifestation.SummonAround(Pawn, Props.volleyEscortCount, HornetSummonRole.Escort),
            };
        }
        #endregion

        #region 내부 도우미
        internal void InterruptCurrentJob()
        {
            if (parent.Spawned && Pawn.jobs?.curJob != null)
            {
                Pawn.jobs.EndCurrentJob(JobCondition.InterruptForced);
            }
        }

        internal void SpawnEffecter(EffecterDef effecterDef, Vector3 offset = default)
        {
            if (effecterDef == null || !parent.Spawned) { return; }

            ApocryphaGimmicks.PlayEffect(effecterDef, parent.Position, parent.Map, (Pawn.DrawPos - parent.Position.ToVector3Shifted()).WithY(0f) + offset);
        }
        #endregion
    }
}
