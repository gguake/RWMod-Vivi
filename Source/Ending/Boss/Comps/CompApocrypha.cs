using RimWorld;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using Verse;
using Verse.AI;

namespace VVRace
{
    public enum ApocryphaPhase : byte
    {
        Queen = 1,
        Rebirth = 2,
        Asphodel = 3,
        Dying = 4,
    }

    public partial class CompApocrypha : ThingComp, IRoofCollapseAlert
    {
        private const int RoofCollapseEscapeRadius = 15;
        private const int StealTargetSkipCheckInterval = 250;
        private const int SwarmCheckInterval = 120;

        private static readonly Color DamageTextColor = new Color(1f, 0.72f, 0.4f);
        private static readonly Color ShieldDamageTextColor = new Color(0.55f, 0.85f, 1f);

        private const int BlockedTextMoteInterval = 60;
        private const int DamageTextInterval = 30;
        private const float QuoteHeight = 2.4f;
        private const float QuoteSecs = 5f;

        private ApocryphaPhase _phase = ApocryphaPhase.Queen;
        private bool _initialized;
        private float _vitality;
        private float _maxVitality;
        private float _threatScale = 1f;
        private float _threatPoints;
        private bool _pendingDeath;
        private int _nextApproachTick;
        private float _approachRadius = -1f;
        private int _nextMeleeTick;
        private float _manaShield;
        private bool _shieldBroken;
        private bool _pendingShieldBreak;
        private int _shieldRestoreTick = -1;

        private int _nextStealTargetSkipCheckTick;

        private ApocryphaTargeting _targeting;
        private ApocryphaNeedleAttack _needleAttack;
        private ApocryphaBreathAttack _breath;
        private ApocryphaChanneling _channel;
        private ApocryphaDoomWave _doomWave;
        private ApocryphaEscortSummon _escortSummon;
        private ApocryphaBlastBreach _blastBreach;
        private ApocryphaFairyBond _bond;
        private ApocryphaFairySwarm _swarm;
        private ApocryphaFairyChains _fairyChains;
        private ApocryphaResurrectionEmbrace _embrace;
        private ApocryphaBondShatter _bondShatter;

        private int _lastBlockedTextTick = -99999;
        private float _pendingDamageText;
        private int _lastDamageTextTick = -99999;
        private int _lastStunImmuneTextTick = -99999;

        internal static bool TryInterceptKill(Pawn pawn)
        {
            var comp = pawn.TryGetComp<CompApocrypha>();
            if (comp == null || comp._phase == ApocryphaPhase.Dying) { return false; }

            if (comp._phase == ApocryphaPhase.Queen)
            {
                comp._vitality = 0f;
                comp._pendingDeath = true;
            }
            else if (comp._phase == ApocryphaPhase.Asphodel && pawn.Spawned)
            {
                comp.TryThrowStatusText(ref comp._lastBlockedTextTick, 1f, LocalizeString_Etc.VV_TextMote_DamageBlockedByFairy.Translate());
            }

            return true;
        }

        public CompProperties_Apocrypha Props => (CompProperties_Apocrypha)props;

        public Pawn Pawn => (Pawn)parent;

        public ApocryphaPhase Phase => _phase;

        public ApocryphaTargeting Targeting => _targeting;
        public ApocryphaNeedleAttack NeedleAttack => _needleAttack;
        public ApocryphaBreathAttack Breath => _breath;
        public ApocryphaChanneling Channel => _channel;
        public ApocryphaDoomWave DoomWave => _doomWave;
        public ApocryphaEscortSummon EscortSummon => _escortSummon;
        public ApocryphaBlastBreach BlastBreach => _blastBreach;
        public ApocryphaFairyBond Bond => _bond;
        public ApocryphaFairySwarm Swarm => _swarm;
        public ApocryphaFairyChains FairyChains => _fairyChains;
        public ApocryphaResurrectionEmbrace Embrace => _embrace;
        public ApocryphaBondShatter BondShatter => _bondShatter;

        public float BondFraction => _bond.Fraction;
        public bool IsAsphodelActive => _phase == ApocryphaPhase.Asphodel && parent.Spawned;
        public int ShieldRestoreTick => _shieldRestoreTick;

        public int ResolveStacks
        {
            get
            {
                if (_phase != ApocryphaPhase.Asphodel) { return 0; }

                var hediff = Pawn.health?.hediffSet?.GetFirstHediffOfDef(VVHediffDefOf.VV_ApocryphaResolve);
                return hediff != null ? Mathf.Clamp(Mathf.RoundToInt(hediff.Severity), 0, Props.resolveMaxStacks) : 0;
            }
        }

        public float ShieldIncomingDamageFactor => Mathf.Max(0.1f, Pawn.GetStatValue(StatDefOf.IncomingDamageFactor));
        public float NeedleDamageFactor => 1f + ResolveStacks * Props.resolveNeedleDamagePerStack;
        public float NeedleAimTimeFactor => Mathf.Max(0.25f, 1f - ResolveStacks * Props.resolveNeedleAimSpeedPerStack);
        public int EffectiveNeedleAimTicks => Mathf.Max(15, Mathf.RoundToInt(Props.needleAimTicks * NeedleAimTimeFactor));

        public float ManaShield => _manaShield;
        public float MaxManaShield => Props.manaShieldMax * FairyBondDifficultyFactor;

        public float HitPointsDifficultyFactor => Mathf.Max(0.1f, Props.hitPointsFactorByThreatScale?.Evaluate(_threatScale) ?? 1f);
        public float FairyBondDifficultyFactor => Mathf.Max(0.1f, Props.fairyBondFactorByThreatScale?.Evaluate(_threatScale) ?? 1f);
        public float DoomBloomTimeFactor => Mathf.Max(0.1f, Props.doomBloomTimeFactorByThreatScale?.Evaluate(_threatScale) ?? 1f);

        public int ArrivalHornetCount => ScaledHornetCount(Props.arrivalHornetCount);

        public int ScaledHornetCount(int baseCount)
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
                if (_phase != ApocryphaPhase.Queen) { return 0f; }

                var table = Props.damageFactorByStuckNeedles;
                if (table.NullOrEmpty()) { return 1f; }

                return table[Mathf.Clamp(_needleAttack.StuckNeedleCount, 0, table.Count - 1)];
            }
        }

        public bool IsActive => _phase == ApocryphaPhase.Queen || _phase == ApocryphaPhase.Asphodel;

        public bool IsQuestBossFight
        {
            get
            {
                var quest = ViviEndingQuest.Current;
                return quest != null && quest.Stage == ViviEndingStage.Boss && quest.Boss == Pawn;
            }
        }
        public int NextApproachTick => _nextApproachTick;
        public float ApproachRadius => _approachRadius;

        public Vector3 BodyCenter => ApocryphaGimmicks.GetBossBodyPos(Pawn);

        public CompApocrypha()
        {
            _targeting = new ApocryphaTargeting(this);
            _needleAttack = new ApocryphaNeedleAttack(this);
            _breath = new ApocryphaBreathAttack(this);
            _channel = new ApocryphaChanneling(this);
            _doomWave = new ApocryphaDoomWave(this);
            _escortSummon = new ApocryphaEscortSummon(this);
            _blastBreach = new ApocryphaBlastBreach(this);
            _bond = new ApocryphaFairyBond(this);
            _swarm = new ApocryphaFairySwarm(this);
            _fairyChains = new ApocryphaFairyChains(this);
            _embrace = new ApocryphaResurrectionEmbrace(this);
            _bondShatter = new ApocryphaBondShatter(this);
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
        }

        private void InitializeState()
        {
            var now = GenTicks.TicksGame;

            _initialized = true;
            _phase = ApocryphaPhase.Queen;
            _threatScale = Find.Storyteller?.difficulty?.threatScale ?? 1f;
            _threatPoints = parent.MapHeld != null ? StorytellerUtility.DefaultThreatPointsNow(parent.MapHeld) : 0f;
            _maxVitality = Props.maxVitality * HitPointsDifficultyFactor;
            _vitality = _maxVitality;
            _manaShield = MaxManaShield;

            _nextApproachTick = now + Props.approachIntervalTicks.RandomInRange;

            _doomWave.SetReadyTick(now + Props.firstDoomWaveDelayTicks);
            _escortSummon.SetPeriodicTick(now + Props.escortSummonIntervalTicks.RandomInRange);
            _breath.SetReadyTick(now);
        }

        private void SyncLifeStage()
        {
            if (_phase == ApocryphaPhase.Dying) { return; }

            var maxIndex = Pawn.RaceProps.lifeStageAges.Count - 1;
            Pawn.ageTracker.LockCurrentLifeStageIndex(Mathf.Clamp(_phase == ApocryphaPhase.Asphodel ? 1 : 0, 0, maxIndex));
        }

        private void SyncPhaseHediffs()
        {
            var def = VVHediffDefOf.VV_ApocryphaFairyBond;
            var shouldHave = _phase == ApocryphaPhase.Asphodel;
            var hediff = Pawn.health.hediffSet.GetFirstHediffOfDef(def);
            if (shouldHave && hediff == null)
            {
                Pawn.health.AddHediff(def);
            }
            else if (!shouldHave && hediff != null)
            {
                Pawn.health.RemoveHediff(hediff);
            }

            if (!shouldHave)
            {
                var resolve = Pawn.health.hediffSet.GetFirstHediffOfDef(VVHediffDefOf.VV_ApocryphaResolve);
                if (resolve != null)
                {
                    Pawn.health.RemoveHediff(resolve);
                }
            }

            _needleAttack.SyncStuckNeedleHediff();
        }

        public override void PostExposeData()
        {
            base.PostExposeData();

            Scribe_Values.Look(ref _phase, "phase", ApocryphaPhase.Queen);
            Scribe_Values.Look(ref _initialized, "initialized", false);
            Scribe_Values.Look(ref _vitality, "vitality", 0f);
            Scribe_Values.Look(ref _maxVitality, "maxVitality", 0f);
            Scribe_Values.Look(ref _threatScale, "threatScale", 1f);
            Scribe_Values.Look(ref _threatPoints, "threatPoints", 0f);
            Scribe_Values.Look(ref _pendingDeath, "pendingDeath", false);
            Scribe_Values.Look(ref _nextApproachTick, "nextApproachTick", 0);
            Scribe_Values.Look(ref _approachRadius, "approachRadius", -1f);
            Scribe_Values.Look(ref _nextMeleeTick, "nextMeleeTick", 0);
            Scribe_Values.Look(ref _manaShield, "manaShield", 0f);
            Scribe_Values.Look(ref _shieldBroken, "shieldBroken", false);
            Scribe_Values.Look(ref _pendingShieldBreak, "pendingShieldBreak", false);
            Scribe_Values.Look(ref _shieldRestoreTick, "shieldRestoreTick", -1);

            Scribe_Deep.Look(ref _targeting, "targeting", this);
            Scribe_Deep.Look(ref _needleAttack, "needleAttack", this);
            Scribe_Deep.Look(ref _breath, "breath", this);
            Scribe_Deep.Look(ref _channel, "channel", this);
            Scribe_Deep.Look(ref _doomWave, "doomWave", this);
            Scribe_Deep.Look(ref _escortSummon, "escortSummon", this);
            Scribe_Deep.Look(ref _blastBreach, "blastBreach", this);
            Scribe_Deep.Look(ref _bond, "bond", this);
            Scribe_Deep.Look(ref _swarm, "swarm", this);
            Scribe_Deep.Look(ref _fairyChains, "fairyChains", this);
            Scribe_Deep.Look(ref _embrace, "embrace", this);

            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                _fairyChains ??= new ApocryphaFairyChains(this);
            }
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
                if (_phase == ApocryphaPhase.Queen)
                {
                    StartRebirth();
                }
                else
                {
                    StartDying();
                }
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
                if (_phase == ApocryphaPhase.Asphodel)
                {
                    BreakShield();
                }
            }

            var now = GenTicks.TicksGame;
            _targeting.Tick(now);

            switch (_phase)
            {
                case ApocryphaPhase.Queen:
                    TickQueen(now);
                    break;

                case ApocryphaPhase.Asphodel:
                    TickAsphodel(now);
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

        private void TickQueen(int now)
        {
            var stunned = Pawn.stances.stunner.Stunned;

            _needleAttack.TickQueen(now, stunned);

            if (IsQuestBossFight)
            {
                _doomWave.Tick(now, !stunned);
                _escortSummon.Tick(now, stunned);

                if (!stunned)
                {
                    _targeting.TickQueenEngagement();
                    _targeting.TickEnemyDetection();
                }
            }

            _breath.Tick(now, stunned);

            if (!stunned && !_breath.IsBreathing && !_channel.IsChanneling && !_escortSummon.IsCasting && now >= _nextMeleeTick)
            {
                _nextMeleeTick = now + Props.meleeIntervalTicks;

                var target = FindAdjacentEnemy();
                if (target != null)
                {
                    Pawn.meleeVerbs.TryMeleeAttack(target);
                }
            }
        }

        private void TickAsphodel(int now)
        {
            var stunned = Pawn.stances.stunner.Stunned;

            if (_shieldBroken && now >= _shieldRestoreTick)
            {
                RestoreShield();
            }

            if (!IsQuestBossFight)
            {
                _needleAttack.DestroyAll();
                _blastBreach.Cancel();
                _embrace.Cancel();
                _swarm.Disperse();
                _fairyChains.Clear();
                return;
            }

            if (_approachRadius < 0f && _channel.TryGetStealTarget(out var target, out _, out _))
            {
                EnsureApproachRadius(target);
            }

            if (!stunned)
            {
                _targeting.TickEnemyDetection();
            }

            _embrace.Tick(now, stunned);
            _needleAttack.TickAsphodel(now, stunned);
            _blastBreach.Tick(now, stunned);
            _bond.Tick(now);
            _fairyChains.Tick(now);
            _bondShatter.Tick(now);

            if (!ShieldBroken && Pawn.IsHashIntervalTick(SwarmCheckInterval))
            {
                _swarm.EnsureSummoned();
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
                    if (things[i] is Pawn target && _targeting.IsTargetPawn(target, 1.5f, origin))
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
            if (damageDef == null || _pendingDeath || !IsActive) { return; }

            if (damageDef == DamageDefOf.Stun || damageDef == DamageDefOf.NerveStun)
            {
                HandleStunDamage(dinfo);
                return;
            }

            if (!damageDef.harmsHealth) { return; }

            _targeting.NoteAttacker(dinfo.Instigator);

            if (_phase == ApocryphaPhase.Asphodel)
            {
                ApplyShieldDamage(dinfo);
                return;
            }

            TakeVitalityDamage(dinfo.Amount * IncomingDamageFactor);
        }

        private void HandleStunDamage(DamageInfo dinfo)
        {
            var damageDef = dinfo.Def;
            var stunTicks = damageDef.constantStunDurationTicks ?? Mathf.RoundToInt(dinfo.Amount * StunHandler.StunDurationTicksPerDamage);
            if (stunTicks <= 0) { return; }

            if (DamageWorker_ManawaveStun.Applying)
            {
                GimmickStun(stunTicks, dinfo.Instigator);
                _channel.Interrupt();
                _embrace.Interrupt();
            }
            else
            {
                Notify_StunBlocked();
            }
        }

        private void ApplyShieldDamage(DamageInfo dinfo)
        {
            if (ShieldBroken || _manaShield <= 0f)
            {
                TryThrowStatusText(ref _lastBlockedTextTick, 1f, LocalizeString_Etc.VV_TextMote_DamageBlockedByFairy.Translate());
                return;
            }

            var effectiveDamage = dinfo.Amount * ShieldIncomingDamageFactor;
            var absorbedByShield = Mathf.Min(_manaShield, effectiveDamage);
            if (absorbedByShield <= 0f) { return; }

            _manaShield -= absorbedByShield;
            _pendingDamageText += absorbedByShield;

            var maxShield = MaxManaShield;
            if (maxShield > 0f && Props.shieldDamageBondLoss > 0f)
            {
                DrainBondMaxFraction(absorbedByShield / maxShield * Props.shieldDamageBondLoss);
                _bondShatter.Trigger(dinfo.Angle);
            }

            if (_manaShield <= 0f)
            {
                _manaShield = 0f;
                _pendingShieldBreak = true;
            }
        }

        private void BreakShield()
        {
            _manaShield = 0f;
            _shieldBroken = true;
            _shieldRestoreTick = GenTicks.TicksGame + Props.shieldRestoreTicks;

            _needleAttack.CancelAims();
            _channel.Interrupt();
            _embrace.Interrupt();
            _swarm.Disperse();
            GimmickStun(Props.shieldBreakStunTicks, null);

            SpawnEffecter(VVEffecterDefOf.VV_Effecter_ApocryphaShieldBreak, ApocryphaGimmicks.GetBossBodyOffset(Pawn));
            ApocryphaUtility.ShakeCamera(parent.Map, 1.2f);

            if (Props.shieldBreakBondLoss > 0f)
            {
                DrainBondMaxFraction(Props.shieldBreakBondLoss);
                _bondShatter.Trigger(Rand.Range(0f, 360f));
            }

            if (IsQuestBossFight && !_pendingDeath)
            {
                var emptySlots = Props.maxActiveFairyChains - _fairyChains.ActiveChainCount;
                if (emptySlots > 0)
                {
                    SpawnFairyFlowers(emptySlots);
                }
            }

            Messages.Message(LocalizeString_Message.VV_Message_ApocryphaShieldBroken.Translate(), Pawn, MessageTypeDefOf.PositiveEvent);
        }

        private void RestoreShield()
        {
            _shieldBroken = false;
            _shieldRestoreTick = -1;
            _manaShield = MaxManaShield;
            _swarm.EnsureSummoned();

            SpawnEffecter(VVEffecterDefOf.VV_Effecter_ApocryphaShieldRestore, ApocryphaGimmicks.GetBossBodyOffset(Pawn));
            Messages.Message(LocalizeString_Message.VV_Message_ApocryphaShieldRestored.Translate(), Pawn, MessageTypeDefOf.NeutralEvent);
        }

        private void SpawnFairyFlowers(int count)
        {
            if (count <= 0) { return; }

            var map = parent.Map;
            foreach (var cell in ApocryphaGimmicks.FindBloomCells(Pawn, count, Props.fairyFlowerDistance))
            {
                var flower = (Building_FairyFlower)ThingMaker.MakeThing(VVThingDefOf.VV_FairyFlower);
                flower.SuppressAutoChain = true;
                ApocryphaGimmicks.SpawnFlower(flower, cell, map, Pawn);
                _fairyChains.CreateChain(flower);
            }
        }

        public void AddResolveStack()
        {
            if (!IsAsphodelActive || _pendingDeath) { return; }

            var hediff = Pawn.health.hediffSet.GetFirstHediffOfDef(VVHediffDefOf.VV_ApocryphaResolve);
            if (hediff == null)
            {
                hediff = Pawn.health.AddHediff(VVHediffDefOf.VV_ApocryphaResolve);
                hediff.Severity = 1f;
            }
            else
            {
                hediff.Severity = Mathf.Min(Props.resolveMaxStacks, Mathf.RoundToInt(hediff.Severity) + 1);
            }

            Messages.Message(LocalizeString_Message.VV_Message_ApocryphaResolveGained.Translate(Mathf.RoundToInt(hediff.Severity)), Pawn, MessageTypeDefOf.NegativeEvent);
        }

        internal void GimmickStun(int ticks, Thing instigator)
        {
            Pawn.stances.stunner.StunFor(ticks, instigator, addBattleLog: false);
        }

        internal void Notify_StunBlocked()
        {
            if (!parent.Spawned) { return; }

            TryThrowStatusText(ref _lastStunImmuneTextTick, 1.4f, LocalizeString_Etc.VV_TextMote_ApocryphaStunImmune.Translate());
        }

        internal void ThrowQuote(string text)
        {
            if (!parent.Spawned) { return; }

            MoteMaker.ThrowText(BodyCenter + new Vector3(0f, 0f, QuoteHeight), parent.Map, text, QuoteSecs);
        }

        private void TryThrowStatusText(ref int lastTick, float zOffset, string text)
        {
            var now = GenTicks.TicksGame;
            if (now - lastTick < BlockedTextMoteInterval) { return; }

            lastTick = now;
            var drawPos = BodyCenter;
            MoteMaker.ThrowText(new Vector3(drawPos.x + 1f, drawPos.y, drawPos.z + zOffset), parent.Map, text);
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
                _phase == ApocryphaPhase.Asphodel ? ShieldDamageTextColor : DamageTextColor);
        }
        #endregion

        #region 계약 API
        internal void TakeVitalityDamage(float amount)
        {
            if (_phase != ApocryphaPhase.Queen || _pendingDeath || amount <= 0f) { return; }

            _vitality = Mathf.Max(0f, _vitality - amount);
            _pendingDamageText += amount;
            if (_vitality <= 0f)
            {
                _pendingDeath = true;
            }
        }

        public void DrainBond(float amount)
        {
            if (!IsAsphodelActive || _pendingDeath) { return; }

            _bond.Drain(amount);
        }

        public void DrainBondMaxFraction(float fraction)
        {
            if (!IsAsphodelActive || _pendingDeath) { return; }

            _bond.DrainMaxFraction(fraction);
        }

        public void RecoverBond(float fraction)
        {
            if (!IsAsphodelActive || _pendingDeath) { return; }

            _bond.Recover(fraction);
        }

        public void RecoverBondMaxFraction(float fraction, bool showStream = true)
        {
            if (!IsAsphodelActive || _pendingDeath) { return; }

            _bond.RecoverMaxFraction(fraction, showStream);
        }

        internal void Notify_BondExhausted()
        {
            if (_phase == ApocryphaPhase.Asphodel)
            {
                _pendingDeath = true;
            }
        }

        public void Notify_Approached()
        {
            _nextApproachTick = GenTicks.TicksGame + Props.approachIntervalTicks.RandomInRange;
        }

        internal void EnsureApproachRadius(Thing target)
        {
            if (_approachRadius >= 0f || target == null || !parent.Spawned) { return; }

            _approachRadius = parent.Position.DistanceTo(target.Position);
        }

        internal void AdvanceApproach(Thing target)
        {
            EnsureApproachRadius(target);
            _approachRadius = Mathf.Max(0f, _approachRadius - Props.approachDistance);
            Notify_Approached();
        }

        internal void SyncApproachRadiusAfterSkip(Thing target, float stealRange)
        {
            if (target == null || !parent.Spawned) { return; }

            var currentDistance = parent.Position.DistanceTo(target.Position);
            _approachRadius = Mathf.Max(stealRange + 1.5f, currentDistance);
        }

        public void GetApproachRing(float stealRange, out float minRadius, out float maxRadius)
        {
            var radius = Mathf.Max(0f, _approachRadius);
            var halfWidth = Mathf.Max(1f, Props.approachRingHalfWidth);
            maxRadius = radius + halfWidth;

            if (radius <= stealRange)
            {
                minRadius = 0f;
            }
            else
            {
                var safeMin = stealRange + 1.5f;
                minRadius = Mathf.Max(safeMin, radius - halfWidth);
                if (maxRadius < minRadius)
                {
                    maxRadius = minRadius + halfWidth;
                }
            }
        }
        #endregion

        #region 페이즈 전환
        private void StealEverflower()
        {
            _channel.Reset();
            if (!_channel.TryGetStealTarget(out var target, out _, out var everflower)) { return; }

            var map = parent.Map;
            CancelAllAttacks();

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
            ApocryphaGimmicks.CleanupAll(map);

            ApocryphaUtility.ReleaseEscorts(Pawn);
            ViviEndingUtility.VanishAllTitanicHornets(map);
            HornetManifestation.CancelAll(map);
            GenSpawn.Spawn(stolen, parent.Position, map);
            Pawn.Destroy(DestroyMode.Vanish);
            CheckMusicTransitions();
        }

        private void StartRebirth()
        {
            if (_phase != ApocryphaPhase.Queen || !parent.Spawned) { return; }

            var map = parent.Map;
            var cell = parent.Position;

            _phase = ApocryphaPhase.Rebirth;
            _pendingShieldBreak = false;
            _blastBreach.Cancel();
            _channel.Reset();
            _breath.Cancel();
            _escortSummon.Cancel();
            _doomWave.Clear();
            _targeting.ClearEngageTarget();
            _needleAttack.DropAll();
            ApocryphaUtility.ClearEscortFocus(Pawn);

            SpawnEffecter(VVEffecterDefOf.VV_Effecter_ApocryphaQueenDeath, ApocryphaGimmicks.GetBossBodyOffset(Pawn));
            var center = BodyCenter;
            EndingVfxUtility.SpawnShockWave(map, center, VVFleckDefOf.VV_Fleck_ApocryphaShockWave, EndingVfxUtility.ShockWaveHalfSize(EndingVfxUtility.MaxDistanceToMapCorner(map, center)));
            ApocryphaUtility.ShakeCamera(map, 2.5f);

            HornetManifestation.CancelAll(map);
            ApocryphaUtility.DissolvePhantasms(map);
            ApocryphaGimmicks.CleanupAll(map);

            var rebirth = (ApocryphaRebirth)ThingMaker.MakeThing(VVThingDefOf.VV_ApocryphaRebirth);
            rebirth.Init(Pawn);
            GenSpawn.Spawn(rebirth, cell, map, WipeMode.Vanish);
            rebirth.Hold(Pawn);

            CheckMusicTransitions();
            Find.TickManager.slower.SignalForceNormalSpeedShort();
        }

        internal void Notify_Reborn()
        {
            if (_phase != ApocryphaPhase.Rebirth || !parent.Spawned) { return; }

            var now = GenTicks.TicksGame;

            _phase = ApocryphaPhase.Asphodel;
            _manaShield = MaxManaShield;
            _shieldBroken = false;
            _pendingShieldBreak = false;
            _shieldRestoreTick = -1;
            _approachRadius = -1f;
            _nextApproachTick = now + Props.approachIntervalTicks.RandomInRange;
            _needleAttack.SetReadyTick(now + Props.needleFirstDelayTicks);
            _embrace.Cancel();
            _embrace.SetReadyTick(now + Props.embraceFirstDelayTicks);
            _bond.Reset();
            _fairyChains.Clear();
            if (_channel.TryGetStealTarget(out var stealTarget, out _, out _))
            {
                EnsureApproachRadius(stealTarget);
            }

            Pawn.stances.stunner.StopStun();
            Pawn.ChangeKind(VVPawnKindDefOf.VV_ApocryphaAsphodel);
            SyncLifeStage();
            SyncPhaseHediffs();
            InterruptCurrentJob();
            Pawn.Drawer?.renderer?.SetAllGraphicsDirty();

            _needleAttack.RecallAll();
            _swarm.EnsureSummoned();

            SpawnEffecter(VVEffecterDefOf.VV_Effecter_ApocryphaTransform, ApocryphaGimmicks.GetBossBodyOffset(Pawn));
            ApocryphaUtility.ShakeCamera(parent.Map, 1.5f);
            CheckMusicTransitions();

            Find.LetterStack.ReceiveLetter(
                LocalizeString_Letter.VV_Letter_ApocryphaRebirthLabel.Translate(),
                LocalizeString_Letter.VV_Letter_ApocryphaRebirth.Translate(),
                LetterDefOf.ThreatBig,
                new LookTargets(Pawn));
        }

        private void StartDying()
        {
            if (_phase == ApocryphaPhase.Dying || !parent.Spawned) { return; }

            var map = parent.Map;
            var dying = (DyingApocrypha)ThingMaker.MakeThing(VVThingDefOf.VV_DyingApocrypha);
            dying.InitFrom(Pawn);

            _phase = ApocryphaPhase.Dying;
            _fairyChains.SettlePendingDispelsBeforeCleanup();
            CancelAllAttacks();
            GenSpawn.Spawn(dying, parent.Position, map);

            ApocryphaUtility.ReleaseEscorts(Pawn);
            ViviEndingUtility.VanishAllTitanicHornets(map);
            HornetManifestation.CancelAll(map);
            ApocryphaGimmicks.CleanupAll(map);

            Find.TickManager.slower.SignalForceNormalSpeedShort();
            Pawn.Destroy(DestroyMode.Vanish);
            CheckMusicTransitions();
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

            _needleAttack.CancelAims();
            _blastBreach.Cancel();
            TeleportTo(cell, endCurrentJob: false);
            return true;
        }
        #endregion

        #region 그리기/UI
        public override void PostDraw()
        {
            base.PostDraw();
            if (!parent.Spawned) { return; }

            ApocryphaGimmicks.DrawLinks(Pawn);

            if (_phase == ApocryphaPhase.Asphodel)
            {
                ApocryphaShroudGlow.Draw(Pawn);
                _fairyChains.Draw();
                _embrace.Draw();
            }

            _channel.Draw();
        }

        public override string CompInspectStringExtra()
        {
            var sb = new StringBuilder();

            if (_phase == ApocryphaPhase.Asphodel)
            {
                if (ShieldBroken)
                {
                    sb.AppendInNewLine(LocalizeString_Inspector.VV_Inspector_ApocryphaShieldBroken.Translate(
                        Mathf.Max(0, _shieldRestoreTick - GenTicks.TicksGame).ToStringTicksToPeriod()));
                }
                else
                {
                    sb.AppendInNewLine(LocalizeString_Inspector.VV_Inspector_ApocryphaShield.Translate(
                        Mathf.CeilToInt(ManaShield).ToString("N0"),
                        Mathf.CeilToInt(MaxManaShield).ToString("N0")));
                }

                sb.AppendInNewLine(BondLabel);
            }
            else if (_phase == ApocryphaPhase.Queen)
            {
                sb.AppendInNewLine(LocalizeString_Inspector.VV_Inspector_ApocryphaVitality.Translate(
                    Mathf.CeilToInt(_vitality).ToString("N0"),
                    Mathf.CeilToInt(MaxVitality).ToString("N0")));
            }

            if (_channel.IsChanneling)
            {
                sb.AppendInNewLine(LocalizeString_Inspector.VV_Inspector_ApocryphaChanneling.Translate(
                    _channel.Progress.ToStringPercent(),
                    _channel.TicksLeft.ToStringTicksToPeriod()));
            }

            if (_embrace.IsCasting)
            {
                sb.AppendInNewLine(LocalizeString_Inspector.VV_Inspector_ApocryphaEmbrace.Translate(
                    _embrace.Progress.ToStringPercent(),
                    _embrace.TicksLeft.ToStringTicksToPeriod()));
            }

            return sb.ToString();
        }

        public string BondLabel => LocalizeString_Inspector.VV_Inspector_ApocryphaFairyBond.Translate(_bond.Fraction.ToStringPercent("0.0"));
        #endregion

        #region 내부 도우미
        private void CancelAllAttacks()
        {
            _channel.Reset();
            _breath.Cancel();
            _escortSummon.Cancel();
            _doomWave.Clear();
            _needleAttack.DestroyAll();
            _blastBreach.Cancel();
            _embrace.Cancel();
            _swarm.Disperse();
            _fairyChains.Clear();
        }

        private static void CheckMusicTransitions()
        {
            var music = Find.MusicManagerPlay;
            if (music != null && !music.disabled)
            {
                music.CheckTransitions();
            }
        }

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

            var drawOffset = EndingVfxUtility.OffsetFromCell(Pawn.DrawPos, parent.Position);
            ApocryphaGimmicks.PlayEffect(effecterDef, parent.Position, parent.Map, drawOffset + offset);
        }
        #endregion
    }
}
