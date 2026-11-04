using RimWorld;
using RPEF;
using Waylith;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace VVRace
{
    public enum FairyState : byte
    {
        Materializing,
        Idle,
        Teleporting,
        Attacking,
        Dematerializing,
    }

    public enum FairyRole : byte
    {
        None,
        Guard,
        Expansion,
    }

    public class ViviFairy : ThingWithComps
    {
        public const int MaterializeDurationTicks = 90;
        public const int DematerializeDurationTicks = 60;
        public const int TeleportDurationTicks = 20;

        private const int LifespanHardCapExtraTicks = 5000;
        private const float DrawCatchUpCellsPerSecond = 28f;
        private const float DrawSnapDistance = 8f;

        private Pawn _owner;
        private int _spawnTick;
        private int _lifespanTicks = 30000;

        private FairyState _state = FairyState.Materializing;
        private FairyJob _job;

        private Vector3 _realPosition;
        private Vector3 _realDirection = Vector3.forward;
        [Unsaved]
        private bool _visualPositionInitialized;
        [Unsaved]
        private Vector3 _visualPosition;
        [Unsaved]
        private int _lastVisualFrame = -1;
        private Rot4 _facing = Rot4.South;

        // 궤도 변주는 thingIDNumber를 시드로 매번 같은 값이 나오므로 저장하지 않는다.
        [Unsaved]
        private bool _orbitVariationInitialized;
        [Unsaved]
        private int _orbitDirection = 1;
        [Unsaved]
        private float _orbitPhaseOffset;
        [Unsaved]
        private float _orbitSpeedFactor = 1f;
        [Unsaved]
        private float _orbitRadiusXFactor = 1f;
        [Unsaved]
        private float _orbitRadiusZFactor = 1f;
        [Unsaved]
        private float _orbitTiltAngle;
        [Unsaved]
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
        public FairyRole Role => CurrentJob?.Role ?? FairyRole.None;
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
        // 작업이 끝난 요정은 다음 틱에 대기 작업을 받으므로 대기 중으로 본다.
        public bool IsAvailable => _state == FairyState.Idle && (CurrentJob == null || CurrentJob.Kind == FairyJobKind.Idle);
        public bool LifespanExpired => GenTicks.TicksGame - _spawnTick >= _lifespanTicks;


        public CompViviHolder Controller => _owner != null ? _owner.GetComp<CompViviHolder>() : null;
        public FairyJob CurrentJob => _job != null && !_job.Ended ? _job : null;

        public override int UpdateRateTicks => 1;

        private void EnsureJob()
        {
            if (_job == null || _job.Ended)
            {
                StartJob(new FairyJob_Idle(_owner));
            }
        }

        public void StartJob(FairyJob job)
        {
            if (job == null)
            {
                job = new FairyJob_Idle(_owner);
            }

            if (_job != null && !_job.Ended && _job != job)
            {
                _job.Notify_ReplacedToAnotherJob();
            }

            _job = job;
            _job.Notify_AssignedToFairy(this);
            _job.CurrentToil?.Start();
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
                var p = SmoothedDrawPosition();
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
                _realPosition = ClampPositionToMap(_realPosition);
            }
            SnapVisualPositionToReal();
        }

        internal void EnterIdle()
        {
            if (Scribe.mode == LoadSaveMode.Inactive)
            {
                TrailRenderer?.FinishTrail();
            }

            _attackGraphicActive = false;
            _state = FairyState.Idle;
        }

        public void BeginDematerialize(bool applyAssimilation)
        {
            if (_state == FairyState.Dematerializing) { return; }

            if (_job != null && !_job.Ended)
            {
                if (_job.Kind != FairyJobKind.Dematerialize)
                {
                    _job.Interrupt(applyAssimilation ?
                        FairyJobInterruptReason.LifespanExpired :
                        FairyJobInterruptReason.DematerializeAll);

                    // OnInterrupted가 이미 소멸 작업을 시작했다면 여기서 또 시작하면 페이즈 이펙트가 중복 재생된다.
                    if (_job != null && !_job.Ended && _job.Kind == FairyJobKind.Dematerialize)
                    {
                        return;
                    }
                }
                else
                {
                    return;
                }
            }

            StartJob(new FairyJob_Dematerialize(_owner, applyAssimilation));
        }

        public void TeleportTo(IntVec3 cell)
        {
            var map = Map;
            if (map == null) { return; }
            if (!cell.IsValid || !cell.InBounds(map))
            {
                cell = ClampCellToMap(cell, map);
            }

            _attackGraphicActive = false;
            PlayPhaseEffectAt(CurrentEffectCell(), map, VVSoundDefOf.VV_FairyTeleport);
            Position = cell;
            _realPosition = cell.ToVector3Shifted().Yto0();
            _drawAltitude = null;
            SnapVisualPositionToReal();
            PlayPhaseEffectAt(cell, map, VVSoundDefOf.VV_FairyTeleport);

            _state = FairyState.Teleporting;
        }

        internal void StartTimedState(FairyState state, bool playPhaseEffect)
        {
            _drawAltitude = null;
            _attackGraphicActive = state == FairyState.Attacking;
            _state = state;

            if (playPhaseEffect)
            {
                var map = Map;
                if (map == null) { return; }

                PlayPhaseEffectAt(CurrentEffectCell(), map, PhaseSoundForState(state));
            }
        }

        internal void BeginAttackMotion()
        {
            _drawAltitude = null;
            _state = FairyState.Attacking;
            _attackGraphicActive = true;
        }

        internal void SetToilPosition(Vector3 position, Vector3 direction, float? drawAltitude = null)
        {
            if (!_visualPositionInitialized)
            {
                SnapVisualPositionToReal();
            }

            var map = Map;
            _realPosition = map != null ? ClampPositionToMap(position) : position.Yto0();
            _drawAltitude = drawAltitude;
            if (direction.sqrMagnitude > 0.0001f)
            {
                _realDirection = direction.normalized;
                _facing = Rot4.FromAngleFlat(_realDirection.AngleFlat());
            }

            var cell = _realPosition.ToIntVec3();
            if (map != null && cell.InBounds(map) && cell != Position)
            {
                Position = cell;
            }
        }

        internal void RegisterToilTrail(Vector3 position)
        {
            TrailRenderer?.RegisterNewTrail(ClampPositionToMap(position));
        }

        internal Vector3 ClampPositionToMap(Vector3 position)
        {
            position = position.Yto0();
            var map = Map;
            if (map == null) { return position; }

            var cell = position.ToIntVec3();
            if (cell.IsValid && cell.InBounds(map))
            {
                return position;
            }

            return ClampCellToMap(cell, map).ToVector3Shifted().Yto0();
        }

        internal void ImpactToilTarget(Thing hitThing)
        {
            if (hitThing == null || !hitThing.Spawned || hitThing.Map != Map || hitThing == _owner) { return; }

            var psychicSensitivity = _owner != null ? Mathf.Clamp(_owner.GetStatValue(StatDefOf.PsychicSensitivity), 0.5f, 10f) : 1f;
            var damageBase = Mathf.Clamp(Mathf.Sqrt(psychicSensitivity), 1f, 4f);
            var actualDamage = (int)damageBase + (Rand.Chance(damageBase - (int)damageBase) ? 1 : 0);

            var sourceDef = def;
            var log = _owner != null ? new BattleLogEntry_FairyImpact(_owner, hitThing, sourceDef) : null;
            if (log != null)
            {
                Find.BattleLog.Add(log);
            }

            var dinfo = new DamageInfo(DamageDefOf.Stab, actualDamage, 0f, _realDirection.AngleFlat(), _owner, null, sourceDef, DamageInfo.SourceCategory.ThingOrUnknown, hitThing);
            var result = hitThing.TakeDamage(dinfo);
            if (log != null)
            {
                result.AssociateWithLog(log);
            }

            if (result.totalDamageDealt > 0 && hitThing is Pawn hitPawn)
            {
                var stagger = hitPawn.stances?.stagger;
                if (stagger != null)
                {
                    stagger.StaggerFor(30);
                }
            }
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

        private Vector3 SmoothedDrawPosition()
        {
            var target = _realPosition.Yto0();
            if (!_visualPositionInitialized)
            {
                SnapVisualPositionToReal();
                return target;
            }

            int frame = Waylith.Time.FrameCount;
            if (_lastVisualFrame == frame)
            {
                return _visualPosition;
            }

            _lastVisualFrame = frame;
            var diff = target - _visualPosition;
            float sqrDistance = diff.sqrMagnitude;
            if (sqrDistance <= 0.0001f || sqrDistance >= DrawSnapDistance * DrawSnapDistance)
            {
                _visualPosition = target;
                return _visualPosition;
            }

            float tickRate = Find.TickManager != null ? Mathf.Max(1f, Find.TickManager.TickRateMultiplier) : 1f;
            float maxStep = DrawCatchUpCellsPerSecond * tickRate * Waylith.Time.DeltaTime;
            if (maxStep > 0f)
            {
                _visualPosition = Vector3.MoveTowards(_visualPosition, target, maxStep);
            }
            return _visualPosition;
        }

        private void SnapVisualPositionToReal()
        {
            _visualPosition = _realPosition.Yto0();
            _visualPositionInitialized = true;
            _lastVisualFrame = Waylith.Time.FrameCount;
        }

        private static SoundDef PhaseSoundForState(FairyState state)
        {
            switch (state)
            {
                case FairyState.Materializing:
                    return VVSoundDefOf.VV_FairyMaterialize;
                case FairyState.Dematerializing:
                    return VVSoundDefOf.VV_FairyDematerialize;
                default:
                    return null;
            }
        }

        private static void PlayPhaseEffectAt(IntVec3 cell, Map map, SoundDef soundDef = null)
        {
            if (map == null || !cell.IsValid || !cell.InBounds(map)) { return; }

            soundDef?.PlayOneShot(SoundInfo.InMap(new TargetInfo(cell, map), MaintenanceType.None));

            var effecter = VVEffecterDefOf.VV_Effecter_FairyPhase;
            if (effecter != null)
            {
                effecter.Spawn(cell, map).Cleanup();
            }
        }

        private static IntVec3 ClampCellToMap(IntVec3 cell, Map map)
        {
            if (map == null) { return cell; }

            int x = Mathf.Clamp(cell.x, 0, map.Size.x - 1);
            int z = Mathf.Clamp(cell.z, 0, map.Size.z - 1);
            return new IntVec3(x, 0, z);
        }

        private void EnsureOrbitVariation()
        {
            if (_orbitVariationInitialized)
            {
                return;
            }

            Rand.PushState(Gen.HashCombineInt(thingIDNumber, 7919));
            try
            {
                _orbitDirection = Rand.Value < 0.5f ? -1 : 1;
                _orbitPhaseOffset = Rand.Range(0f, Mathf.PI * 2f);
                _orbitSpeedFactor = Rand.Range(0.72f, 1.28f);
                _orbitRadiusXFactor = Rand.Range(0.82f, 1.18f);
                _orbitRadiusZFactor = Rand.Range(0.82f, 1.18f);
            }
            finally
            {
                Rand.PopState();
            }

            Rand.PushState(Gen.HashCombineInt(thingIDNumber, 104729));
            try
            {
                _orbitTiltAngle = Rand.Range(0f, Mathf.PI);
                _motionCurveOffsetFactor = Rand.Range(-0.85f, 0.85f);
                if (Mathf.Abs(_motionCurveOffsetFactor) < 0.25f)
                {
                    _motionCurveOffsetFactor += _motionCurveOffsetFactor < 0f ? -0.25f : 0.25f;
                }
            }
            finally
            {
                Rand.PopState();
            }

            _orbitVariationInitialized = true;
        }

        protected override void TickInterval(int delta)
        {
            base.TickInterval(delta);

            if (_state != FairyState.Dematerializing && (_owner == null || !_owner.Spawned || _owner.Destroyed || _owner.Dead || _owner.Map != Map))
            {
                BeginDematerialize(false);
            }

            bool lifespanHardCapExpired = GenTicks.TicksGame - _spawnTick >= _lifespanTicks + LifespanHardCapExtraTicks;
            if (_state != FairyState.Dematerializing && LifespanExpired && (IsAvailable || lifespanHardCapExpired))
            {
                BeginDematerialize(true);
            }

            EnsureJob();
            _job.Tick(delta);
            UpdateFacing();
        }

        private void UpdateFacing()
        {
            // 이동/공격 중에는 진행 방향을 향한다. 대기 궤도 중의 방향은 OrbitIdleStep이 접선 방향으로 갱신한다.
            if (_state == FairyState.Attacking)
            {
                if (_realDirection.sqrMagnitude > 0.0001f)
                {
                    _facing = Rot4.FromAngleFlat(_realDirection.AngleFlat());
                }
            }

            Rotation = _facing;
        }

        public override void Destroy(DestroyMode mode = DestroyMode.Vanish)
        {
            Controller?.Notify_FairyDestroyed(this);
            base.Destroy(mode);
        }

        public override void ExposeData()
        {
            base.ExposeData();

            Scribe_References.Look(ref _owner, "owner");
            Scribe_Values.Look(ref _spawnTick, "spawnTick");
            Scribe_Values.Look(ref _lifespanTicks, "lifespanTicks", 30000);

            Scribe_Values.Look(ref _state, "state", FairyState.Idle);
            Scribe_Deep.Look(ref _job, "job");

            Scribe_Values.Look(ref _realPosition, "realPosition");
            Scribe_Values.Look(ref _realDirection, "realDirection", Vector3.forward);
            Scribe_Values.Look(ref _facing, "facing");

            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                // 이동/공격 상태는 저장된 토일이 그대로 이어가므로 언세이브 필드인 공격 그래픽만 복원한다.
                if (_state == FairyState.Attacking)
                {
                    _attackGraphicActive = true;
                }
                // 작업과 토일의 요정 참조는 저장되지 않으므로 다시 연결한다. 끝난 작업은 다음 틱에 교체된다.
                CurrentJob?.Notify_AssignedToFairy(this);
            }
        }
    }
}
