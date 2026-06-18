using RimWorld;
using RPEF;
using System.Reflection;
using UnityEngine;
using Verse;

namespace VVRace
{
    public enum FairyState : byte
    {
        Materializing,
        Idle,
        Teleporting,
        MovingToRest,
        Attacking,
        Dematerializing,
    }

    public enum FairyRole : byte
    {
        None,
        Guard,
        Concentration,
        Expansion,
    }

    public class ViviFairy : ThingWithComps
    {
        public const int MaterializeDurationTicks = 90;
        public const int DematerializeDurationTicks = 60;
        public const int TeleportDurationTicks = 20;

        private const int LifespanHardCapExtraTicks = 5000;

        private Pawn _owner;
        private int _spawnTick;
        private int _lifespanTicks = 30000;

        private FairyState _state = FairyState.Materializing;
        private int _stateTicks;
        private FairyJob _job;

        private Vector3 _realPosition;
        private Vector3 _realDirection = Vector3.forward;
        private Rot4 _facing = Rot4.South;
        private bool _orbitVariationInitialized;
        private int _orbitDirection = 1;
        private float _orbitPhaseOffset;
        private float _orbitSpeedFactor = 1f;
        private float _orbitRadiusXFactor = 1f;
        private float _orbitRadiusZFactor = 1f;
        private bool _orbitShapeVariationInitialized;
        private float _orbitTiltAngle;
        private float _motionCurveOffsetFactor;
        [Unsaved]
        private float? _drawAltitude;

        // 돌진 공격 상태.
        [Unsaved]
        private bool _attackGraphicActive;
        [Unsaved]
        private Graphic _attackGraphicCached;
        [Unsaved]
        private CompTrailRenderer _trailRenderer;

        public Pawn Owner => _owner;
        public FairyState State => _state;
        public FairyRole Role => CurrentJob.Role;
        internal Vector3 RealPosition => _realPosition.Yto0();
        internal Vector3 RealDirection => _realDirection;
        internal int OrbitDirection { get { EnsureOrbitVariation(); return _orbitDirection; } }
        internal float OrbitPhaseOffset { get { EnsureOrbitVariation(); return _orbitPhaseOffset; } }
        internal float OrbitSpeedFactor { get { EnsureOrbitVariation(); return _orbitSpeedFactor; } }
        internal float OrbitRadiusXFactor { get { EnsureOrbitVariation(); return _orbitRadiusXFactor; } }
        internal float OrbitRadiusZFactor { get { EnsureOrbitVariation(); return _orbitRadiusZFactor; } }
        internal float OrbitTiltAngle { get { EnsureOrbitVariation(); return _orbitTiltAngle; } }
        internal float MotionCurveOffsetFactor { get { EnsureOrbitVariation(); return _motionCurveOffsetFactor; } }

        public bool InAction => Role != FairyRole.None;
        public bool IsAvailable => _state == FairyState.Idle && CurrentJob.Kind == FairyJobKind.Idle;
        public bool LifespanExpired => GenTicks.TicksGame - _spawnTick >= _lifespanTicks;


        public CompViviFairyController Controller => _owner != null ? _owner.GetComp<CompViviFairyController>() : null;
        public FairyJob CurrentJob
        {
            get
            {
                EnsureJob();
                return _job;
            }
        }

        public override int UpdateRateTicks => 1;

        internal void EnsureJob()
        {
            if (_job == null || _job.Ended)
            {
                StartJob(new FairyJob_Idle(_owner));
                return;
            }

            _job.NotifyAssigned(this);
        }

        public void StartJob(FairyJob job)
        {
            if (job == null)
            {
                job = new FairyJob_Idle(_owner);
            }

            if (_job != null && !_job.Ended && _job != job)
            {
                _job.NotifyReplaced();
            }

            _job = job;
            _job.NotifyAssigned(this);
            _job.StartCurrentToil();
        }

        private CompTrailRenderer TrailRenderer
        {
            get
            {
                if (_trailRenderer == null) { _trailRenderer = GetComp<CompTrailRenderer>(); }
                return _trailRenderer;
            }
        }

        private Graphic AttackGraphic
        {
            get
            {
                if (_attackGraphicCached == null)
                {
                    var data = def.graphicData;
                    var shader = (data != null && data.shaderType != null) ? data.shaderType.Shader : ShaderDatabase.MoteGlow;
                    _attackGraphicCached = GraphicDatabase.Get<Graphic_Multi>(
                        "Things/Mote/VV_FairyAttack/VV_FairyAttack",
                        shader,
                        data != null ? data.drawSize : new Vector2(0.7f, 0.7f),
                        data != null ? data.color : new Color(1f, 1f, 1f, 0.85f));
                }
                return _attackGraphicCached;
            }
        }

        public override Graphic Graphic
        {
            get
            {
                if (_state == FairyState.Attacking || _attackGraphicActive)
                {
                    return AttackGraphic;
                }
                return base.Graphic;
            }
        }

        public override Vector3 DrawPos
        {
            get
            {
                // 그 외(행동중/순간이동/생성·소멸/세션 배정 등)는 _realPosition + MoteOverhead 수준으로.
                var p = _realPosition;
                p.y = _drawAltitude ?? AltitudeLayer.MoteOverhead.AltitudeFor();
                return p;
            }
        }

        public void Initialize(Pawn owner, int lifespanTicks)
        {
            _owner = owner;
            _lifespanTicks = lifespanTicks;
            _spawnTick = GenTicks.TicksGame;
            EnsureOrbitVariation();
        }

        public override void SpawnSetup(Map map, bool respawningAfterLoad)
        {
            base.SpawnSetup(map, respawningAfterLoad);

            if (!respawningAfterLoad)
            {
                _realPosition = Position.ToVector3Shifted().Yto0();
            }
            else
            {
                Controller?.RegisterFairy(this);
            }
        }

        internal void EnterIdle()
        {
            if (Scribe.mode == LoadSaveMode.Inactive)
            {
                TrailRenderer?.ResetTrail();
            }
            _attackGraphicActive = false;
            _state = FairyState.Idle;
            _stateTicks = 0;
        }

        public void BeginDematerialize(bool applyAssimilation)
        {
            if (_state == FairyState.Dematerializing) { return; }

            if (_job != null && !_job.Ended && _job.Kind != FairyJobKind.Dematerialize)
            {
                _job.Interrupt(applyAssimilation ? FairyJobInterruptReason.LifespanExpired : FairyJobInterruptReason.DematerializeAll);
            }
            if (_job != null && !_job.Ended && _job.Kind == FairyJobKind.Dematerialize)
            {
                return;
            }
            StartJob(new FairyJob_Dematerialize(_owner, applyAssimilation));
        }

        public void TeleportTo(IntVec3 cell)
        {
            var map = Map;
            if (map == null) { return; }

            _attackGraphicActive = false;
            PlayPhaseEffectAt(CurrentEffectCell(), map);
            Position = cell;
            _realPosition = cell.ToVector3Shifted().Yto0();
            _drawAltitude = null;
            PlayPhaseEffectAt(cell, map);

            _state = FairyState.Teleporting;
            _stateTicks = TeleportDurationTicks;
        }

        internal void FaceTowards(Vector3 targetPosition)
        {
            var direction = (targetPosition - _realPosition).Yto0();
            if (direction.sqrMagnitude <= 0.0001f)
            {
                return;
            }

            _realDirection = direction.normalized;
            _facing = Rot4.FromAngleFlat(_realDirection.AngleFlat());
            Rotation = _facing;
        }

        internal void StartTimedState(FairyState state, int durationTicks, bool playPhaseEffect)
        {
            _drawAltitude = null;
            _attackGraphicActive = state == FairyState.Attacking;
            _state = state;
            _stateTicks = durationTicks;

            if (playPhaseEffect)
            {
                var map = Map;
                if (map == null) { return; }

                PlayPhaseEffectAt(CurrentEffectCell(), map);
            }
        }

        internal void SetTimedStateTicks(int ticks)
        {
            _stateTicks = ticks;
        }

        internal void BeginToilMotion(FairyState state)
        {
            if (state != FairyState.Attacking && state != FairyState.MovingToRest) { return; }

            _drawAltitude = null;
            _state = state;
            if (state == FairyState.Attacking)
            {
                _attackGraphicActive = true;
            }
        }

        internal void SetToilPosition(Vector3 position, Vector3 direction, float? drawAltitude = null)
        {
            _realPosition = position.Yto0();
            _drawAltitude = drawAltitude;
            if (direction.sqrMagnitude > 0.0001f)
            {
                _realDirection = direction.normalized;
                _facing = Rot4.FromAngleFlat(_realDirection.AngleFlat());
            }

            var map = Map;
            var cell = _realPosition.ToIntVec3();
            if (map != null && cell.InBounds(map) && cell != Position)
            {
                Position = cell;
            }
        }

        internal void RegisterToilTrail(Vector3 position)
        {
            TrailRenderer?.RegisterNewTrail(position);
        }

        internal void ImpactToilTarget(Thing hitThing)
        {
            if (hitThing == null || !hitThing.Spawned || hitThing.Map != Map || hitThing == _owner) { return; }

            float psy = _owner != null ? Mathf.Clamp(_owner.GetStatValue(StatDefOf.PsychicSensitivity), 0.5f, 10f) : 1f;
            int dmg = Mathf.Max(1, Mathf.RoundToInt(1f * psy));
            float angle = _realDirection.AngleFlat();

            var dinfo = new DamageInfo(DamageDefOf.Stab, dmg, 0f, angle, _owner, null, null, DamageInfo.SourceCategory.ThingOrUnknown, hitThing);
            hitThing.TakeDamage(dinfo);
        }

        private IntVec3 CurrentEffectCell()
        {
            var cell = _realPosition.ToIntVec3();
            var map = Map;
            if (map != null && cell.IsValid && cell.InBounds(map))
            {
                return cell;
            }

            return Position;
        }

        private static void PlayPhaseEffectAt(IntVec3 cell, Map map)
        {
            if (map == null || !cell.IsValid || !cell.InBounds(map)) { return; }

            var effecter = VVEffecterDefOf.VV_Effecter_FairyPhase;
            if (effecter != null)
            {
                effecter.Spawn(cell, map).Cleanup();
            }
        }

        private void EnsureOrbitVariation()
        {
            if (_orbitVariationInitialized && _orbitShapeVariationInitialized)
            {
                return;
            }

            if (!_orbitVariationInitialized)
            {
                Rand.PushState(Gen.HashCombineInt(thingIDNumber, 7919));
                try
                {
                    _orbitDirection = Rand.Value < 0.5f ? -1 : 1;
                    _orbitPhaseOffset = Rand.Range(0f, Mathf.PI * 2f);
                    _orbitSpeedFactor = Rand.Range(0.72f, 1.28f);
                    _orbitRadiusXFactor = Rand.Range(0.82f, 1.18f);
                    _orbitRadiusZFactor = Rand.Range(0.82f, 1.18f);
                    _orbitVariationInitialized = true;
                }
                finally
                {
                    Rand.PopState();
                }
            }

            if (!_orbitShapeVariationInitialized)
            {
                Rand.PushState(Gen.HashCombineInt(thingIDNumber, 104729));
                try
                {
                    _orbitTiltAngle = Rand.Range(0f, Mathf.PI);
                    _motionCurveOffsetFactor = Rand.Range(-0.85f, 0.85f);
                    if (Mathf.Abs(_motionCurveOffsetFactor) < 0.25f)
                    {
                        _motionCurveOffsetFactor += _motionCurveOffsetFactor < 0f ? -0.25f : 0.25f;
                    }
                    _orbitShapeVariationInitialized = true;
                }
                finally
                {
                    Rand.PopState();
                }
            }
        }

        protected override void TickInterval(int delta)
        {
            base.TickInterval(delta);

            // owner가 사라졌거나 다른 맵으로 이동/사망하면 안전하게 소멸. (동화 헤디프는 남기지 않음)
            if (_state != FairyState.Dematerializing && (_owner == null || !_owner.Spawned || _owner.Destroyed || _owner.Dead || _owner.Map != Map))
            {
                BeginDematerialize(false);
            }

            // 수명 만료 처리: 자유 대기 상태(미배정)일 때만 소멸. 세션에 배정된 요정은 세션이 처리한다.
            // 단 너무 오래 행동중이면(안전장치) 강제 소멸.
            bool lifespanHardCapExpired = GenTicks.TicksGame - _spawnTick >= _lifespanTicks + LifespanHardCapExtraTicks;
            if (_state != FairyState.Dematerializing && LifespanExpired && (IsAvailable || lifespanHardCapExpired))
            {
                BeginDematerialize(true);
            }

            Controller?.Notify_FairyTick(this);

            CurrentJob.Tick(delta);
            UpdateFacing();
        }

        // 회전 각도(라디안). 시계는 초 단위이므로 틱당 각속도(orbitAngularSpeed)에 60을 곱해 초당 각속도로 환산.
        // 그리기(궤도 시각)는 DrawPos가 매 프레임 처리한다. 여기서는 논리 위치(_realPosition/셀)와
        // 바라보는 방향만 동기화한다. (대기→돌진 전이 시작점을 현재 궤도 위치에 맞추고 4방향 스프라이트 방향 갱신)
        private void UpdateFacing()
        {
            // 이동/공격 중에는 진행 방향을 향한다. 대기 궤도 중의 방향은 OrbitIdleStep이 접선 방향으로 갱신한다.
            if (_state == FairyState.Attacking || _state == FairyState.MovingToRest)
            {
                if (_realDirection.sqrMagnitude > 0.0001f)
                {
                    _facing = Rot4.FromAngleFlat(_realDirection.AngleFlat());
                }
            }

            Rotation = _facing;
        }

        internal void ApplyAssimilationFromJob()
        {
            if (_owner == null || _owner.Dead || _owner.health == null) { return; }

            var hediff = _owner.health.hediffSet.GetFirstHediffOfDef(VVHediffDefOf.VV_EverflowerAssimilation);
            if (hediff == null)
            {
                hediff = _owner.health.AddHediff(VVHediffDefOf.VV_EverflowerAssimilation);
            }
            else
            {
                hediff.Severity = Mathf.Min(hediff.Severity + 1f, hediff.def.maxSeverity);
            }
        }

        public override void DeSpawn(DestroyMode mode = DestroyMode.Vanish)
        {
            var ctrl = Controller;
            base.DeSpawn(mode);
            ctrl?.Notify_FairyGone(this);
        }

        public override void ExposeData()
        {
            base.ExposeData();

            Scribe_References.Look(ref _owner, "owner");
            Scribe_Values.Look(ref _spawnTick, "spawnTick");
            Scribe_Values.Look(ref _lifespanTicks, "lifespanTicks", 30000);

            Scribe_Values.Look(ref _state, "state", FairyState.Idle);
            Scribe_Values.Look(ref _stateTicks, "stateTicks");
            Scribe_Deep.Look(ref _job, "job");

            Scribe_Values.Look(ref _realPosition, "realPosition");
            Scribe_Values.Look(ref _realDirection, "realDirection", Vector3.forward);
            Scribe_Values.Look(ref _facing, "facing");
            Scribe_Values.Look(ref _orbitVariationInitialized, "orbitVariationInitialized");
            Scribe_Values.Look(ref _orbitDirection, "orbitDirection", 1);
            Scribe_Values.Look(ref _orbitPhaseOffset, "orbitPhaseOffset");
            Scribe_Values.Look(ref _orbitSpeedFactor, "orbitSpeedFactor", 1f);
            Scribe_Values.Look(ref _orbitRadiusXFactor, "orbitRadiusXFactor", 1f);
            Scribe_Values.Look(ref _orbitRadiusZFactor, "orbitRadiusZFactor", 1f);
            Scribe_Values.Look(ref _orbitShapeVariationInitialized, "orbitShapeVariationInitialized");
            Scribe_Values.Look(ref _orbitTiltAngle, "orbitTiltAngle");
            Scribe_Values.Look(ref _motionCurveOffsetFactor, "motionCurveOffsetFactor");

            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                if (_orbitDirection == 0)
                {
                    _orbitVariationInitialized = false;
                }
                if (!_orbitVariationInitialized || !_orbitShapeVariationInitialized)
                {
                    EnsureOrbitVariation();
                }
                if (_state == FairyState.Attacking || _state == FairyState.MovingToRest)
                {
                    EnterIdle();
                }
                EnsureJob();
            }
        }
    }
}
