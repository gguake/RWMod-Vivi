using RimWorld;
using RPEF;
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

        // 대기 시 이동 관련 상수. (궤도 모양/속도는 CompProperties_ViviFairyController로 이동)
        private const float IdleFollowSpeed = 0.1f;        // 따라잡는 속도(셀/틱)
        private const float IdleTeleportDistance = 9f;      // 이보다 멀어지면 순간이동으로 복귀

        // 행동중인데도 수명이 지나 대기로 못 돌아오는 경우를 대비한 강제 소멸 여유분.
        private const int LifespanHardCapExtraTicks = 5000;

        // 돌진 공격(베지어) 관련 상수. Needle 구현을 차용.
        private const float RefreshInterval = 25f;
        private const float BezierWeight = 20f;
        public const float DefaultDashSpeed = 60f;
        private const int StaggerStunAmount = 12;

        private Pawn _owner;
        private int _spawnTick;
        private int _lifespanTicks = 30000;

        private FairyState _state = FairyState.Materializing;
        private int _stateTicks;
        private bool _wantsDematerialize;
        private bool _applyAssimilationOnDematerialize = true;
        private FairyRole _role = FairyRole.None;

        private Vector3 _realPosition;
        private Vector3 _realDirection = Vector3.forward;
        private Rot4 _facing = Rot4.South;

        // 돌진 공격 상태.
        private Thing _curTargetThing;
        private Vector3 _restPosition;
        private Vector3 _moveStartPosition;
        private Vector3 _moveEndPosition;
        private Vector3 _curDirectionOutVector;
        private Vector3 _curDirectionInVector;
        private float _totalMoveDistance;
        private float _curMoveDistance;
        private float _appliedSpeed;

        [Unsaved]
        private Graphic _attackGraphicCached;
        [Unsaved]
        private CompTrailRenderer _trailRenderer;
        // 자유 대기 궤도 슬롯(컨트롤러가 매 틱 배정). 위치 계산은 요정 자신이 매 틱 수행.
        [Unsaved]
        private int _orbitSlot;
        [Unsaved]
        private int _orbitCount = 1;
        [Unsaved]
        private Pawn _drawOrbitCenter;

        public Pawn Owner => _owner;
        public FairyState State => _state;
        public FairyRole Role => _role;
        // 능력에 배정된 요정은 '행동중'으로 간주(Gizmo 빨강, 다른 능력에 사용 불가).
        public bool InAction => _role != FairyRole.None;
        // 새 명령에 사용 가능한 요정: 대기 상태이며 어떤 세션에도 배정되지 않음.
        public bool IsAvailable => _state == FairyState.Idle && _role == FairyRole.None;
        public bool LifespanExpired => GenTicks.TicksGame - _spawnTick >= _lifespanTicks;

        public CompViviFairyController Controller => _owner != null ? _owner.GetComp<CompViviFairyController>() : null;

        // 매 틱 갱신(기본은 카메라 거리 기반 저빈도 틱이라 느린 회전이 끊겨 보임 → 항상 1로 고정).
        public override int UpdateRateTicks => 1;

        public void SetRole(FairyRole role)
        {
            _role = role;
            if (role != FairyRole.Guard)
            {
                _drawOrbitCenter = null;
            }
        }

        public void SetOrbitSlot(int slot, int count)
        {
            _orbitSlot = slot;
            _orbitCount = Mathf.Max(1, count);
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
                if (_state == FairyState.Attacking)
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
                var props = Controller?.Props;
                if (IsAvailable && props != null && _owner != null && _owner.Spawned && _owner.Map == Map)
                {
                    float angle = OrbitAngle(props, GenTicks.TicksGame);
                    float cos = Mathf.Cos(angle);
                    float sin = Mathf.Sin(angle);

                    Vector3 center = _owner.DrawPos;   // 폰의 프레임 보간 위치(이동 중에도 부드럽게 추종)
                    var orbit = center.Yto0() + new Vector3(cos * props.orbitRadiusX, 0f, sin * props.orbitRadiusZ);

                    return ApplyOrbitDrawDepth(orbit, center, props);
                }

                // 그 외(행동중/순간이동/생성·소멸/세션 배정 등)는 _realPosition + MoteOverhead 수준으로.
                var p = _realPosition;
                if (_state == FairyState.Idle && props != null && _drawOrbitCenter != null && _drawOrbitCenter.Spawned && _drawOrbitCenter.Map == Map)
                {
                    return ApplyOrbitDrawDepth(p, _drawOrbitCenter.DrawPos, props);
                }

                p.y = AltitudeLayer.MoteOverhead.AltitudeFor();
                return p;
            }
        }

        private Vector3 ApplyOrbitDrawDepth(Vector3 orbit, Vector3 center, CompProperties_ViviFairyController props)
        {
            float radiusZ = Mathf.Max(0.0001f, props.orbitRadiusZ);
            float rel = Mathf.Clamp((center.z - orbit.z) / radiusZ, -1f, 1f);
            float t = 0.5f + 0.5f * rel; // 0 = 뒤(북), 1 = 앞(남)
            orbit.y = center.y + Mathf.Lerp(-props.orbitDepth, props.orbitDepth, t);
            return orbit;
        }

        public void Initialize(Pawn owner, int lifespanTicks)
        {
            _owner = owner;
            _lifespanTicks = lifespanTicks;
            _spawnTick = GenTicks.TicksGame;
        }

        public override void SpawnSetup(Map map, bool respawningAfterLoad)
        {
            base.SpawnSetup(map, respawningAfterLoad);

            if (!respawningAfterLoad)
            {
                _realPosition = this.TrueCenter();
            }
            else
            {
                // 로드 시 컨트롤러 목록 복원 백업(멱등).
                Controller?.NotifyFairyRestored(this);
            }
        }

        public void BeginMaterialize()
        {
            _state = FairyState.Materializing;
            _stateTicks = MaterializeDurationTicks;
            PlayPhaseEffect();
        }

        private void EnterIdle()
        {
            _state = FairyState.Idle;
            _stateTicks = 0;
        }

        // 컨트롤러가 매 틱 호출. 대기 상태 요정을 주어진 대형 위치로 부드럽게 이동시킨다.
        // 거리가 너무 멀면(주인 순간이동 등) 순간이동으로 복귀한다.
        public void IdleFollowStep(Vector3 target, int delta)
        {
            if (_state != FairyState.Idle || Map == null) { return; }

            _drawOrbitCenter = null;

            Vector3 cur = _realPosition.Yto0();
            Vector3 tar = target.Yto0();
            Vector3 diff = tar - cur;
            float dist = diff.magnitude;

            if (dist > IdleTeleportDistance)
            {
                var cell = tar.ToIntVec3();
                if (!cell.InBounds(Map))
                {
                    cell = (_owner != null && _owner.Spawned) ? _owner.Position : Position;
                }
                TeleportTo(cell);
                return;
            }

            if (dist > 0.02f)
            {
                float step = IdleFollowSpeed * delta;
                _realPosition += (step >= dist) ? diff : diff.normalized * step;

                var cell = _realPosition.ToIntVec3();
                if (cell.InBounds(Map) && cell != Position)
                {
                    Position = cell;
                }
            }
        }

        public void BeginDematerialize(bool applyAssimilation)
        {
            if (_state == FairyState.Dematerializing) { return; }

            _applyAssimilationOnDematerialize = applyAssimilation;
            _state = FairyState.Dematerializing;
            _stateTicks = DematerializeDurationTicks;
            PlayPhaseEffect();
        }

        // 빈 셀로 순간이동(대기 복귀/포위 배치 등에 사용). Phase 2에서 사용.
        public void TeleportTo(IntVec3 cell)
        {
            if (Map == null) { return; }

            PlayPhaseEffect();
            Position = cell;
            _realPosition = this.TrueCenter();
            PlayPhaseEffect();

            _state = FairyState.Teleporting;
            _stateTicks = TeleportDurationTicks;
        }

        // === 돌진 공격 (세션이 구동) ===

        // 대상에게 빠르게 돌진해 관통 공격하고, 끝나면 restPos로 복귀한다.
        public void StartDashAttack(Thing target, Vector3 restPos, float speed)
        {
            if (Map == null || target == null) { return; }

            _curTargetThing = target;
            _restPosition = restPos.Yto0();
            _appliedSpeed = speed > 0f ? speed : DefaultDashSpeed;
            SetMoveTarget(target.TrueCenter().Yto0());
            _state = FairyState.Attacking;
        }

        // 공격하지 않고 지정 위치로 활주 복귀.
        public void StartReturnTo(Vector3 restPos, float speed)
        {
            if (Map == null) { return; }

            _curTargetThing = null;
            _restPosition = restPos.Yto0();
            _appliedSpeed = speed > 0f ? speed : DefaultDashSpeed;
            SetMoveTarget(_restPosition);
            _state = FairyState.MovingToRest;
        }

        private void SetMoveTarget(Vector3 end)
        {
            _moveStartPosition = _realPosition.Yto0();
            _moveEndPosition = end.Yto0();

            var dir = _moveEndPosition - _moveStartPosition;
            if (dir.sqrMagnitude < 0.0001f) { dir = _realDirection; }
            dir = dir.normalized;

            _curDirectionOutVector = _realDirection.sqrMagnitude > 0.0001f ? _realDirection.normalized : dir;
            _curDirectionInVector = dir;
            _totalMoveDistance = CalculateBezierCurveLengthApproximate(_moveStartPosition, _moveEndPosition, _curDirectionOutVector * BezierWeight, _curDirectionInVector * BezierWeight);
            _curMoveDistance = 0f;
        }

        private void AdvanceDash(int delta)
        {
            float totalCost = delta * _appliedSpeed;
            var position = _realPosition;

            while (totalCost > 0)
            {
                float moves = Mathf.Clamp(RefreshInterval, 0, totalCost);
                bool approach = false;
                if (moves >= _totalMoveDistance - _curMoveDistance)
                {
                    moves = _totalMoveDistance - _curMoveDistance;
                    approach = true;
                }

                if (approach)
                {
                    _realPosition = _moveEndPosition;
                    _realDirection = _curDirectionInVector;

                    if (_state == FairyState.Attacking)
                    {
                        TrailRenderer?.RegisterNewTrail(position);

                        var hit = _curTargetThing;
                        _curTargetThing = null;
                        if (hit != null && hit.Spawned && hit.Map == Map)
                        {
                            Impact(hit);
                        }

                        StartReturnTo(_restPosition, _appliedSpeed);
                    }
                    else
                    {
                        EnterIdle();
                    }
                    return;
                }
                else
                {
                    _curMoveDistance += moves;
                    position = CalculateBezierCurvePoint(_moveStartPosition, _moveEndPosition, _curDirectionOutVector * BezierWeight, _curDirectionInVector * BezierWeight, _curMoveDistance / _totalMoveDistance);
                    if (_state == FairyState.Attacking)
                    {
                        TrailRenderer?.RegisterNewTrail(position);
                    }
                }

                totalCost -= moves;
            }

            var cell = position.ToIntVec3();
            if (!cell.InBounds(Map))
            {
                // 맵 밖이면 파괴하지 않고 복귀로 전환(예외 안전).
                _realPosition = position;
                StartReturnTo(_restPosition, _appliedSpeed);
                return;
            }

            Position = cell;
            _realPosition = position;
            _realDirection = CalculateBezierCurveDerivative(_moveStartPosition, _moveEndPosition, _curDirectionOutVector * BezierWeight, _curDirectionInVector * BezierWeight, _curMoveDistance / _totalMoveDistance);
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

        private const int BezierLengthInterval = 4;
        private float CalculateBezierCurveLengthApproximate(Vector3 p1, Vector3 p2, Vector3 w1, Vector3 w2)
        {
            var distance = 0f;
            var point = p1;
            for (int i = 1; i < BezierLengthInterval; ++i)
            {
                var newPoint = CalculateBezierCurvePoint(p1, p2, w1, w2, i / (float)BezierLengthInterval);
                distance += (newPoint - point).magnitude;
                point = newPoint;
            }

            distance += (p2 - point).magnitude;
            return distance * 100;
        }

        private Vector3 CalculateBezierCurvePoint(Vector3 p1, Vector3 p2, Vector3 w1, Vector3 w2, float t)
        {
            var tInv = 1 - t;
            var c1 = p1 + w1 / 3f;
            var c2 = p2 - w2 / 3f;
            return Mathf.Pow(tInv, 3) * p1 +
                3 * tInv * tInv * t * c1 +
                3 * tInv * t * t * c2 +
                t * t * t * p2;
        }

        private Vector3 CalculateBezierCurveDerivative(Vector3 p1, Vector3 p2, Vector3 w1, Vector3 w2, float t)
        {
            var tInv = 1 - t;
            var c1 = p1 + w1 / 3f;
            var c2 = p2 - w2 / 3f;
            return -3 * tInv * tInv * p1 +
                3 * tInv * (1 - 3 * t) * c1 +
                3 * t * (2 - 3 * t) * c2 +
                3 * t * t * p2;
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
                if ((_state == FairyState.Idle && _role == FairyRole.None) || hardCap)
                {
                    BeginDematerialize(true);
                }
            }

            Controller?.Notify_FairyTick(this, delta);

            UpdateFacing();

            switch (_state)
            {
                case FairyState.Materializing:
                    _stateTicks -= delta;
                    if (_stateTicks <= 0) { EnterIdle(); }
                    break;

                case FairyState.Teleporting:
                    _stateTicks -= delta;
                    if (_stateTicks <= 0) { EnterIdle(); }
                    break;

                case FairyState.Dematerializing:
                    _stateTicks -= delta;
                    if (_stateTicks <= 0)
                    {
                        if (_applyAssimilationOnDematerialize)
                        {
                            ApplyAssimilation();
                        }
                        Destroy();
                    }
                    break;

                case FairyState.Attacking:
                case FairyState.MovingToRest:
                    AdvanceDash(delta);
                    break;

                case FairyState.Idle:
                    // 자유 대기 요정의 궤도 회전은 요정 자신이 매 틱 구동(부드러운 회전).
                    // 세션 배정 요정(role != None)은 세션이 위치를 잡는다.
                    if (_role == FairyRole.None)
                    {
                        DoIdleOrbit();
                    }
                    break;
            }
        }

        public Vector3 OrbitPositionAround(Pawn centerPawn, int slot, int count)
        {
            var props = Controller?.Props;
            if (props == null || centerPawn == null || !centerPawn.Spawned || centerPawn.Map != Map)
            {
                return _realPosition.Yto0();
            }

            _drawOrbitCenter = centerPawn;

            float angle = OrbitAngle(props, GenTicks.TicksGame, slot, count);
            float cos = Mathf.Cos(angle);
            float sin = Mathf.Sin(angle);
            return centerPawn.DrawPos.Yto0() + new Vector3(cos * props.orbitRadiusX, 0f, sin * props.orbitRadiusZ);
        }

        public void IdleOrbitAround(Pawn centerPawn, int slot, int count)
        {
            if (_state != FairyState.Idle || Map == null) { return; }

            var props = Controller?.Props;
            if (props == null || centerPawn == null || !centerPawn.Spawned || centerPawn.Map != Map) { return; }

            _drawOrbitCenter = centerPawn;

            float angle = OrbitAngle(props, GenTicks.TicksGame, slot, count);
            float cos = Mathf.Cos(angle);
            float sin = Mathf.Sin(angle);

            _realPosition = centerPawn.DrawPos.Yto0() + new Vector3(cos * props.orbitRadiusX, 0f, sin * props.orbitRadiusZ);

            var cell = _realPosition.ToIntVec3();
            if (cell.InBounds(Map) && cell != Position) { Position = cell; }

            Vector3 tangent = new Vector3(-sin * props.orbitRadiusX, 0f, cos * props.orbitRadiusZ);
            if (tangent.sqrMagnitude > 0.0001f)
            {
                _realDirection = tangent.normalized;
                _facing = Rot4.FromAngleFlat(_realDirection.AngleFlat());
            }
        }

        // 회전 각도(라디안). 시계는 초 단위이므로 틱당 각속도(orbitAngularSpeed)에 60을 곱해 초당 각속도로 환산.
        private float OrbitAngle(CompProperties_ViviFairyController props, float clockSeconds)
        {
            return OrbitAngle(props, clockSeconds, _orbitSlot, _orbitCount);
        }

        private float OrbitAngle(CompProperties_ViviFairyController props, float clockSeconds, int slot, int count)
        {
            return clockSeconds * props.orbitAngularSpeed + slot * (Mathf.PI * 2f / Mathf.Max(1, count));
        }

        // 그리기(궤도 시각)는 DrawPos가 매 프레임 처리한다. 여기서는 논리 위치(_realPosition/셀)와
        // 바라보는 방향만 동기화한다. (대기→돌진 전이 시작점을 현재 궤도 위치에 맞추고 4방향 스프라이트 방향 갱신)
        private void DoIdleOrbit()
        {
            IdleOrbitAround(_owner, _orbitSlot, _orbitCount);
        }

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

            Scribe_Values.Look(ref _realPosition, "realPosition");
            Scribe_Values.Look(ref _realDirection, "realDirection", Vector3.forward);
            Scribe_Values.Look(ref _facing, "facing");

            Scribe_References.Look(ref _curTargetThing, "curTargetThing");
            Scribe_Values.Look(ref _restPosition, "restPosition");
            Scribe_Values.Look(ref _moveStartPosition, "moveStartPosition");
            Scribe_Values.Look(ref _moveEndPosition, "moveEndPosition");
            Scribe_Values.Look(ref _curDirectionOutVector, "curDirectionOutVector");
            Scribe_Values.Look(ref _curDirectionInVector, "curDirectionInVector");
            Scribe_Values.Look(ref _totalMoveDistance, "totalMoveDistance");
            Scribe_Values.Look(ref _curMoveDistance, "curMoveDistance");
            Scribe_Values.Look(ref _appliedSpeed, "appliedSpeed");
        }
    }
}
