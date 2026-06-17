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

    // 요정이 현재 어떤 능력 세션에 배정되어 있는지. None이면 자유(대기 대형 추종).
    public enum FairyRole : byte
    {
        None,
        Guard,
        Concentration,
        Expansion,
    }

    // 실체화된 요정 비비. 선택/상호작용 불가능한 시각 효과형 엔티티이며 로열 비비(owner)에 귀속된다.
    // Needle(Projectile)의 그리기/이동/저장 패턴을 차용하되, 행동 완료 후 소멸하지 않고 대기 상태로 복귀한다.
    public class ViviFairy : ThingWithComps
    {
        public const int MaterializeDurationTicks = 90;
        public const int DematerializeDurationTicks = 60;
        public const int TeleportDurationTicks = 20;

        // 행동중인데도 수명이 지나 대기로 못 돌아오는 경우를 대비한 강제 소멸 여유분.
        private const int LifespanHardCapExtraTicks = 5000;

        // 돌진 공격 피해 처리. 이동 상태와 속도는 세션이 소유한다.
        private const int StaggerStunAmount = 12;

        private Pawn _owner;
        private int _spawnTick;
        private int _lifespanTicks = 30000;

        private FairyState _state = FairyState.Materializing;
        private int _stateTicks;
        private bool _wantsDematerialize;
        private bool _applyAssimilationOnDematerialize = true;
        private FairyRole _role = FairyRole.None;
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

        private static readonly FieldInfo TrailPointsField = typeof(CompTrailRenderer).GetField("points", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        private static readonly FieldInfo TrailStartField = typeof(CompTrailRenderer).GetField("trailStart", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

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

        // 능력에 배정된 요정은 '행동중'으로 간주(Gizmo 빨강, 다른 능력에 사용 불가).
        public bool InAction => Role != FairyRole.None;
        // 새 명령에 사용 가능한 요정: 대기 상태이며 어떤 세션에도 배정되지 않음.
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

        // 매 틱 갱신(기본은 카메라 거리 기반 저빈도 틱이라 느린 회전이 끊겨 보임 → 항상 1로 고정).
        public override int UpdateRateTicks => 1;

        public void SetRole(FairyRole role)
        {
            _role = role;
        }

        internal void EnsureJob()
        {
            if (_job == null || _job.Ended)
            {
                StartJob(new FairyJob_Idle(_owner));
                return;
            }

            _job.NotifyAssigned(this);
            _role = _job.Role;
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
            _role = _job.Role;
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
                _realPosition = CellCenter(Position);
            }
            else
            {
                // 로드 시 컨트롤러 목록 복원 백업(멱등).
                Controller?.NotifyFairyRestored(this);
            }
        }

        public void BeginMaterialize()
        {
            StartJob(new FairyJob_Materialize(_owner));
        }

        private void EnterIdle()
        {
            if (Scribe.mode == LoadSaveMode.Inactive)
            {
                ClearToilTrail();
            }
            _attackGraphicActive = false;
            _state = FairyState.Idle;
            _stateTicks = 0;
        }

        // 컨트롤러가 매 틱 호출. 대기 상태 요정을 주어진 대형 위치로 부드럽게 이동시킨다.
        // 거리가 너무 멀면(주인 순간이동 등) 순간이동으로 복귀한다.
        public void BeginDematerialize(bool applyAssimilation)
        {
            if (_state == FairyState.Dematerializing) { return; }

            _applyAssimilationOnDematerialize = applyAssimilation;
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

        // 빈 셀로 순간이동(대기 복귀/포위 배치 등에 사용). Phase 2에서 사용.
        public void TeleportTo(IntVec3 cell)
        {
            if (Map == null) { return; }

            _attackGraphicActive = false;
            PlayPhaseEffect();
            Position = cell;
            _realPosition = CellCenter(cell);
            _drawAltitude = null;
            PlayPhaseEffect();

            _state = FairyState.Teleporting;
            _stateTicks = TeleportDurationTicks;
        }

        private static Vector3 CellCenter(IntVec3 cell)
        {
            return cell.ToVector3Shifted().Yto0();
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
                PlayPhaseEffect();
            }
        }

        internal void SetTimedStateTicks(int ticks)
        {
            _stateTicks = ticks;
        }

        // === 세션 구동 이동 API ===

        // 세션 액션이 돌진/복귀 표시 상태를 시작한다.
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

        // 세션 액션이 계산한 실제 위치와 방향을 적용한다.
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

        private void ClearToilTrail()
        {
            var trail = TrailRenderer;
            if (trail == null) { return; }

            var clearPosition = RealPosition;
            var points = TrailPointsField?.GetValue(trail) as Vector3[];
            if (points == null || points.Length == 0)
            {
                return;
            }

            for (int i = 0; i < points.Length; i++)
            {
                points[i] = clearPosition;
            }

            TrailStartField?.SetValue(trail, true);
        }

        internal void ImpactToilTarget(Thing hitThing)
        {
            Impact(hitThing);
        }

        internal void EnterIdleFromToil()
        {
            EnterIdle();
        }

        internal void StopToilMotion()
        {
            if (_state == FairyState.Attacking || _state == FairyState.MovingToRest)
            {
                EnterIdle();
            }
        }

        // 매우 낮은 피해 + 높은 저지력(스턴). 피해는 로열 비비의 정신 감응력에 비례.
        private void Impact(Thing hitThing)
        {
            if (hitThing == null || !hitThing.Spawned || hitThing.Map != Map || hitThing == _owner) { return; }

            float psy = _owner != null ? Mathf.Clamp(_owner.GetStatValue(StatDefOf.PsychicSensitivity), 0.5f, 10f) : 1f;
            int dmg = Mathf.Max(1, Mathf.RoundToInt(Rand.Range(1f, 2f) * psy));
            float angle = _realDirection.AngleFlat();

            var dinfo = new DamageInfo(DamageDefOf.Blunt, dmg, 0f, angle, _owner, null, null, DamageInfo.SourceCategory.ThingOrUnknown, hitThing);
            hitThing.TakeDamage(dinfo);

            if (hitThing is Pawn p && !p.Dead && !p.RaceProps.IsMechanoid)
            {
                var stun = new DamageInfo(DamageDefOf.Stun, StaggerStunAmount, 0f, angle, _owner, null, null, DamageInfo.SourceCategory.ThingOrUnknown, hitThing);
                hitThing.TakeDamage(stun);
            }

            SpawnPierceFlecks(hitThing);
        }

        private void SpawnPierceFlecks(Thing hitThing)
        {
            var map = hitThing.Map;
            if (map == null) { return; }

            var basePos = hitThing.TrueCenter().Yto0();
            basePos.y = AltitudeLayer.MoteOverhead.AltitudeFor();
            if (!basePos.ToIntVec3().ShouldSpawnMotesAt(map)) { return; }

            var direction = _realDirection.Yto0();
            if (direction.sqrMagnitude < 0.0001f) { direction = Vector3.forward; }
            direction.Normalize();
            var angle = direction.AngleFlat();

            var pierceData = FleckMaker.GetDataStatic(basePos, map, VVFleckDefOf.VV_Fleck_NeedlePierce, Rand.Range(0.7f, 0.95f));
            pierceData.rotation = angle;
            map.flecks.CreateFleck(pierceData);
        }

        private void PlayPhaseEffect()
        {
            if (Map == null) { return; }
            var effecter = VVEffecterDefOf.VV_Effecter_FairyPhase;
            if (effecter != null)
            {
                effecter.Spawn(this, Map).Cleanup();
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
            if (_state != FairyState.Dematerializing)
            {
                if (_owner == null || !_owner.Spawned || _owner.Destroyed || _owner.Dead || _owner.Map != Map)
                {
                    BeginDematerialize(false);
                }
            }

            // 수명 만료 처리: 자유 대기 상태(미배정)일 때만 소멸. 세션에 배정된 요정은 세션이 처리한다.
            // 단 너무 오래 행동중이면(안전장치) 강제 소멸.
            if (_state != FairyState.Dematerializing && LifespanExpired)
            {
                _wantsDematerialize = true;
                bool hardCap = GenTicks.TicksGame - _spawnTick >= _lifespanTicks + LifespanHardCapExtraTicks;
                if (IsAvailable || hardCap)
                {
                    BeginDematerialize(true);
                }
            }

            Controller?.Notify_FairyTick(this, delta);

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

        private void ApplyAssimilation()
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

        internal void ApplyAssimilationFromJob()
        {
            ApplyAssimilation();
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
            Scribe_Values.Look(ref _wantsDematerialize, "wantsDematerialize");
            Scribe_Values.Look(ref _applyAssimilationOnDematerialize, "applyAssimilationOnDematerialize", true);
            Scribe_Values.Look(ref _role, "role", FairyRole.None);
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
                    StopToilMotion();
                }
                EnsureJob();
            }
        }
    }
}
