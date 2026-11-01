using RimWorld;
using RPEF;
using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace VVRace
{
    // 아포크리파의 거대 엘텍스 바늘. 보스 둘레에 핀 판넬처럼 떠다니다가(Idle) 적의 위치를 조준하고(Aiming),
    // 조준이 끝나면 그 방향으로 최대 사거리까지 순간적으로 날아가며 넓은 직선 위의 모든 대상을 꿰뚫은 뒤(Firing), 끝에 잠시 박혀 떨다가(Lodged)
    // 엘텍스 바늘처럼 곡선을 그리며 제자리로 돌아온다(Returning).
    // 1페이즈: 보스의 왼쪽 어깨 뒤에 핀 판넬처럼 나란히 꽂혀 있다가, 보스 컴프가 시킬 때 보스 앞의 발사 지점으로 떨어져 나와 쏜다.
    // 2페이즈: 보스를 따라 둘레를 맴돌며 바늘마다 쉬는 시간(ReadyTick)이 끝나면 떠 있는 자리에서 바로 쏜다. 발사 직선 위에 보스가 있으면
    // 피해를 주는 대신 보스에게 꽂혀(Stuck) 멈추고, 보스 컴프가 뽑을 때(PullOut)까지 보스 몸에 붙어 다닌다.
    // 발사 주기·대상 선정은 보스 컴프(CompApocrypha)가, 움직임·피해·연출은 이 Thing이 맡는다. 보스가 전투 페이즈가 아니게 되면 스스로 흩어진다.
    public class ApocryphaNeedle : ThingWithComps
    {
        private enum NeedleState : byte
        {
            Idle = 0,
            Aiming = 1,
            Firing = 2,
            Lodged = 3,
            Returning = 4,
            Stuck = 5,
        }

        // 1페이즈 자리: 건담의 핀 판넬처럼 보스의 왼쪽 어깨 뒤에 바늘을 나란히 겹쳐 세워 한쪽 날개 모양을 이룬다.
        // 안쪽 바늘일수록 곧게 서고 바깥쪽일수록 더 기울며(부채꼴), 밑동은 바깥쪽으로 갈수록 조금씩 아래로 물러난다.
        // 보스가 정면(남)·옆을 보면 몸 뒤에 가려 그리고, 등을 보이면(북) 몸 앞에 그린다.
        private const float FinRootX = 1f;
        private const float FinRootZ = 0.9f;
        private const float FinStepX = 0.7f;
        private const float FinStepZ = -0.3f;
        private const float FinTiltInner = 16f;
        private const float FinTiltStep = 11f;
        private const float FinCenterFromRoot = 2f;
        private const float FinSwayDegrees = 2.5f;
        private const float FinBobHeight = 0.08f;
        private const float FinBehindAltitude = 0.03f;
        private const float FinRackReturnProgress = 0.85f;
        private const float SlotBobHeight = 0.14f;
        private const float IdleFollow = 0.16f;

        // 2페이즈: 몸통 중심 둘레의 타원 궤도를 고르게 나눠 맴돈다(한 바퀴 24초).
        private const float OrbitRadiusX = 5.4f;
        private const float OrbitRadiusZ = 4.4f;
        private const float OrbitDegreesPerTick = 0.25f;

        // 2페이즈: 보스 몸의 맞는 범위(발밑과 몸통 중심 두 점에서 이 반경)와 꽂힌 모양(몸통 중심에서 발사 반대쪽으로 물러난 거리, 바늘끼리 옆으로 벌린 간격)
        private const float BossHitRadius = 1.6f;
        private const float StuckDepth = 1.4f;
        private const float StuckSpread = 0.45f;
        private const int StuckShakeTicks = 14;

        // 조준: 몸통 중심에서 LaunchOffset칸 떨어진 발사 지점으로 DetachTicks 동안 옮겨 가 대상을 겨누고, 조준 끝 무렵 WindupDistance만큼 물러나 떤다.
        // 1페이즈에서 대상이 발사 지점보다 CloseTargetMargin칸 넘게 앞에 있지 않으면(보스에 바짝 붙은 적) 몸통 옆 부채꼴 자리(CloseTargetFanAngle)에서 겨눈다.
        private const float LaunchOffset = 3.2f;
        private const float CloseTargetMargin = 2f;
        private const float CloseTargetFanAngle = 75f;
        private const int DetachTicks = 24;
        private const float WindupDistance = 0.8f;
        private const int ShakeTicks = 24;
        private const float ShakeAmplitude = 0.08f;

        // 발사: 바늘 끝(촉)이 틱당 FireSpeed칸으로 날아가며(32칸 ≈ 5틱) TrailPointSpacing칸마다 궤적 점을 남긴다. 촉이 경고 띠 끝에 닿으면 LodgedTicks 동안 박혀 떤다.
        // NeedleTipOffset: 바늘 그림 중심에서 촉까지(그림 높이의 36% × 5.5칸). 날아가는 동안 촉이 띠 밖으로 나가지 않게 몸통은 그만큼 뒤에 그린다.
        // 피해: 폰은 그려지는 자리(DrawPos)를 중심으로 반경 PawnHitRadius가 띠에 걸치면, 건물은 차지한 칸이 띠에 걸치면 꿰뚫는다(경고 띠와 같은 범위).
        private const float FireSpeed = 7f;
        private const float NeedleTipOffset = 1.98f;
        private const float PawnHitRadius = 0.35f;
        private const float TrailPointSpacing = 1f;
        private const int LodgedTicks = 16;

        // 귀환: 엘텍스 바늘처럼 3차 베지어 곡선을 따라 돌아온다(발사 방향으로 조금 더 나아갔다가 자리 바깥쪽에서 들어온다).
        private const float ReturnSpeed = 0.9f;
        private const float ReturnOutWeight = 7f;
        private const float ReturnInWeight = 4f;
        private const float ReturnBlendStart = 0.75f;

        private const int AimPetalInterval = 4;

        // 발사 왜곡을 그리는 시간(VV_NeedleWarp의 _Duration 0.55초와 같게)
        private const int WarpTicks = 33;

        private const int FadeInTicks = 40;
        private const float MapEdgeMargin = 0.05f;

        private static readonly List<Thing> _tmpCellThings = new List<Thing>();

        // 저장되는 상태
        private Pawn _boss;
        private int _slot;
        private NeedleState _state;
        private Vector3 _pos;
        private float _angle;
        private int _stateStartTick;
        private Vector3 _moveFrom;
        private float _moveFromAngle;
        private Vector3 _laneStart;
        private Vector3 _laneDir;
        private float _laneLength;
        private int _aimTicks;
        private int _fireTick;
        private float _travelled;
        private Vector3 _returnFrom;
        private float _returnLength;
        private float _returnProgress;
        private Vector3 _returnOutDir;
        private List<int> _hitIds = new List<int>();
        private float _stickAlong = -1f;
        private Vector3 _stuckOffset;
        private float _stuckAngle;
        private int _readyTick;

        // 저장하지 않는 상태
        private int _spawnTick;
        private CompApocrypha _bossComp;
        private CompTrailRenderer _trail;
        private List<Thing> _ignored;

        public int Slot => _slot;
        public bool IsIdle => _state == NeedleState.Idle;
        public bool IsAiming => _state == NeedleState.Aiming;
        public bool IsFlying => _state == NeedleState.Firing || _state == NeedleState.Lodged || _state == NeedleState.Returning;
        public bool IsStuck => _state == NeedleState.Stuck;

        // 2페이즈: 이 틱이 지나야 다음 조준을 할 수 있다(보스 컴프가 읽고, 대상이 없으면 미룬다).
        public int ReadyTick
        {
            get => _readyTick;
            set => _readyTick = value;
        }

        // 조준 진행도(0~1). 조준 중이 아니면 0.
        public float AimProgress => IsAiming ? Mathf.Clamp01((GenTicks.TicksGame - _stateStartTick) / (float)Mathf.Max(1, _aimTicks)) : 0f;

        public Vector3 LaneStart => _laneStart;
        public Vector3 LaneEnd => _laneStart + _laneDir * _laneLength;
        public float LaneLength => _laneLength;

        public override Vector3 DrawPos => new Vector3(_pos.x, DrawAltitude, _pos.z);

        // 1페이즈 핀 판넬 자리에 꽂혀 있는지(쉬는 중이거나 자리에 거의 돌아온 귀환 막바지).
        private bool IsRacked => !IsUnbound && (_state == NeedleState.Idle || (_state == NeedleState.Returning && _returnProgress >= FinRackReturnProgress));

        // 등 뒤 핀 판넬은 보스 몸에 가려지도록 보스보다 살짝 낮게 그린다(등을 보일 때와 날아다닐 때는 위에).
        private float DrawAltitude => IsRacked && FinBehindBody ? AltitudeLayer.Pawn.AltitudeFor() - FinBehindAltitude : AltitudeLayer.MoteOverhead.AltitudeFor();

        // 핀 판넬이 달린 쪽(보스의 왼쪽 어깨 뒤)이 화면의 어느 쪽인지: 정면·서향이면 오른쪽(+1), 등·동향이면 왼쪽(-1).
        private float FinSide => _boss != null && (_boss.Rotation == Rot4.South || _boss.Rotation == Rot4.West) ? 1f : -1f;

        private bool FinBehindBody => _boss != null && _boss.Rotation != Rot4.North;

        private CompApocrypha BossComp
        {
            get
            {
                if (_bossComp == null || _bossComp.parent != _boss)
                {
                    _bossComp = _boss?.TryGetComp<CompApocrypha>();
                }

                return _bossComp;
            }
        }

        private CompTrailRenderer Trail => _trail ?? (_trail = GetComp<CompTrailRenderer>());

        private float HalfWidth => BossComp?.Props.needleHalfWidth ?? 1.5f;

        private Vector3 BodyCenter => ApocryphaGimmicks.GetBossBodyPos(_boss).Yto0();

        private float LaneAngle => _laneDir.AngleFlat();

        private bool IsUnbound => BossComp?.Phase == ApocryphaPhase.Unbound;

        public void Init(Pawn boss, int slot)
        {
            _boss = boss;
            _slot = slot;
            _state = NeedleState.Idle;
        }

        public override void SpawnSetup(Map map, bool respawningAfterLoad)
        {
            base.SpawnSetup(map, respawningAfterLoad);

            if (respawningAfterLoad)
            {
                _spawnTick = GenTicks.TicksGame - FadeInTicks;
                return;
            }

            // 처음 나타날 때는 자리에서 흐릿하게 떠오른다.
            _spawnTick = GenTicks.TicksGame;
            if (IsUnbound)
            {
                _readyTick = GenTicks.TicksGame + BossComp.Props.unboundNeedleFirstDelayTicks.RandomInRange;
            }

            if (_boss != null && _boss.Spawned)
            {
                _pos = SlotPos(GenTicks.TicksGame);
                _angle = SlotAngle(GenTicks.TicksGame);
                SyncPosition();
            }
        }

        public override void ExposeData()
        {
            base.ExposeData();

            Scribe_References.Look(ref _boss, "boss");
            Scribe_Values.Look(ref _slot, "slot", 0);
            Scribe_Values.Look(ref _state, "state", NeedleState.Idle);
            Scribe_Values.Look(ref _pos, "pos");
            Scribe_Values.Look(ref _angle, "angle", 0f);
            Scribe_Values.Look(ref _stateStartTick, "stateStartTick", 0);
            Scribe_Values.Look(ref _moveFrom, "moveFrom");
            Scribe_Values.Look(ref _moveFromAngle, "moveFromAngle", 0f);
            Scribe_Values.Look(ref _laneStart, "laneStart");
            Scribe_Values.Look(ref _laneDir, "laneDir");
            Scribe_Values.Look(ref _laneLength, "laneLength", 0f);
            Scribe_Values.Look(ref _aimTicks, "aimTicks", 0);
            Scribe_Values.Look(ref _fireTick, "fireTick", 0);
            Scribe_Values.Look(ref _travelled, "travelled", 0f);
            Scribe_Values.Look(ref _returnFrom, "returnFrom");
            Scribe_Values.Look(ref _returnLength, "returnLength", 0f);
            Scribe_Values.Look(ref _returnProgress, "returnProgress", 0f);
            Scribe_Values.Look(ref _returnOutDir, "returnOutDir");
            Scribe_Collections.Look(ref _hitIds, "hitIds", LookMode.Value);
            Scribe_Values.Look(ref _stickAlong, "stickAlong", -1f);
            Scribe_Values.Look(ref _stuckOffset, "stuckOffset");
            Scribe_Values.Look(ref _stuckAngle, "stuckAngle", 0f);
            Scribe_Values.Look(ref _readyTick, "readyTick", 0);

            if (Scribe.mode == LoadSaveMode.PostLoadInit && _hitIds == null)
            {
                _hitIds = new List<int>();
            }
        }

        #region 보스가 부르는 API
        // 보스 컴프가 고른 대상 칸을 겨눈다. 발사 직선(시작점·방향·길이)은 지금 정하고 조준 동안 바뀌지 않는다.
        // 1페이즈는 보스 몸통 앞의 발사 지점에서, 2페이즈는 바늘이 떠 있는 자리에서 쏜다(그동안 보스는 따로 움직인다).
        // fanAngle(1페이즈 여러 바늘 동시 발사): 보스→대상 방향에서 이만큼 돌린 몸통 둘레 반경 needleFanRadius의 부채꼴 자리에서 대상을 겨눠 쏜다
        // (함께 쏘는 바늘들의 직선이 대상에서 교차한다). null이면 몸통 앞의 기존 발사 지점에서 쏜다.
        public void StartAim(IntVec3 targetCell, int aimTicks, float? fanAngle = null)
        {
            if (!Spawned || _boss == null || !_boss.Spawned) { return; }

            var now = GenTicks.TicksGame;
            var origin = IsUnbound ? _pos.Yto0() : BodyCenter;
            var target = targetCell.ToVector3Shifted().Yto0();
            var direction = (target - origin).Yto0();
            if (direction.sqrMagnitude < 0.01f)
            {
                direction = (_pos - BodyCenter).Yto0();
            }

            direction.Normalize();

            var start = IsUnbound ? origin : origin + direction * LaunchOffset;

            // 1페이즈: 대상이 발사 지점 앞으로 충분히 떨어져 있지 않으면(보스에 바짝 붙은 적) 띠가 대상을 덮지 못하므로,
            // 몸통 옆 부채꼴 자리(바늘이 있는 쪽)에서 대상을 겨눈다.
            if (!IsUnbound && !fanAngle.HasValue && Vector3.Distance(origin, target) < LaunchOffset + CloseTargetMargin)
            {
                var radius = BossComp?.Props.needleFanRadius ?? 5f;
                var left = origin + Quaternion.AngleAxis(-CloseTargetFanAngle, Vector3.up) * direction * radius;
                var right = origin + Quaternion.AngleAxis(CloseTargetFanAngle, Vector3.up) * direction * radius;
                fanAngle = (left - _pos.Yto0()).sqrMagnitude <= (right - _pos.Yto0()).sqrMagnitude ? -CloseTargetFanAngle : CloseTargetFanAngle;
            }

            if (fanAngle.HasValue && !IsUnbound)
            {
                start = origin + Quaternion.AngleAxis(fanAngle.Value, Vector3.up) * direction * (BossComp?.Props.needleFanRadius ?? 5f);
                var toTarget = (target - start).Yto0();
                if (toTarget.sqrMagnitude > 0.01f)
                {
                    direction = toTarget.normalized;
                }
            }

            _laneDir = direction;
            _laneStart = ClampInsideMap(start);
            _laneLength = LaneLengthInsideMap(_laneStart, direction, BossComp?.Props.needleRange ?? 32f);

            _moveFrom = _pos;
            _moveFromAngle = _angle;
            _aimTicks = Mathf.Max(1, aimTicks);
            _fireTick = now + _aimTicks;
            _state = NeedleState.Aiming;
            _stateStartTick = now;

            PlayEffectAt(ApocryphaEffecterDefOf.VV_Effecter_ApocryphaNeedleAim, _pos);
        }

        // 조준 중이면 발사를 취소하고 제자리로 돌아간다(이미 날아간 바늘은 그대로 둔다). 취소했으면 true.
        public bool CancelAim()
        {
            if (_state != NeedleState.Aiming) { return false; }

            _state = NeedleState.Idle;
            _stateStartTick = GenTicks.TicksGame;
            _readyTick = Mathf.Max(_readyTick, GenTicks.TicksGame + 120);
            return true;
        }

        // 2페이즈: 꽂혀 있으면 발사 반대 방향으로 뽑혀 나와 자리로 돌아간다. 뽑았으면 true.
        public bool PullOut()
        {
            if (_state != NeedleState.Stuck || !Spawned) { return false; }

            var now = GenTicks.TicksGame;
            PlayEffectAt(ApocryphaEffecterDefOf.VV_Effecter_ApocryphaNeedlePullOut, _pos);
            Trail?.ResetTrail();
            RegisterTrail(_pos);
            StartReturn(now, _pos, -_laneDir);
            return true;
        }

        // DEV: 쉬고 있는(또는 조준 중인) 바늘을 바로 보스에게 꽂는다.
        public void DebugStick()
        {
            if (!Spawned || !IsUnbound || (_state != NeedleState.Idle && _state != NeedleState.Aiming)) { return; }

            var direction = (BodyCenter - _pos.Yto0()).Yto0();
            _laneDir = direction.sqrMagnitude > 0.01f ? direction.normalized : Vector3.forward;
            Stick(GenTicks.TicksGame);
        }

        // 그 자리에서 꽃잎으로 흩어지며 사라진다.
        public void Vanish()
        {
            if (Destroyed) { return; }

            if (Spawned)
            {
                PlayEffectAt(ApocryphaEffecterDefOf.VV_Effecter_ApocryphaNeedleVanish, _pos);
            }

            Destroy(DestroyMode.Vanish);
        }

        // point(월드 좌표)가 이 바늘의 발사 직선(넓은 띠) 안에 있는지.
        public bool LaneContains(Vector3 point)
        {
            var rel = (point - _laneStart).Yto0();
            var along = rel.x * _laneDir.x + rel.z * _laneDir.z;
            if (along < 0f || along > _laneLength) { return false; }

            return Mathf.Abs(rel.x * _laneDir.z - rel.z * _laneDir.x) <= HalfWidth;
        }
        #endregion

        #region 틱
        protected override void Tick()
        {
            base.Tick();
            if (!Spawned) { return; }

            var comp = BossComp;
            if (comp == null || !_boss.Spawned || _boss.Map != Map || (comp.Phase != ApocryphaPhase.Bound && comp.Phase != ApocryphaPhase.Unbound))
            {
                Vanish();
                return;
            }

            var now = GenTicks.TicksGame;
            switch (_state)
            {
                case NeedleState.Idle:
                    TickIdle(now);
                    break;

                case NeedleState.Aiming:
                    TickAiming(now);
                    break;

                case NeedleState.Firing:
                    TickFiring(now);
                    break;

                case NeedleState.Lodged:
                    TickLodged(now);
                    break;

                case NeedleState.Returning:
                    TickReturning(now);
                    break;

                case NeedleState.Stuck:
                    TickStuck(now);
                    break;
            }

            if (Spawned)
            {
                SyncPosition();
            }
        }

        // 자리를 부드럽게 따라간다(보스가 움직이면 살짝 늦게 따라와 떠 있는 느낌을 준다).
        private void TickIdle(int now)
        {
            _pos = Vector3.Lerp(_pos, SlotPos(now), IdleFollow);
            _angle = LerpAngle(_angle, SlotAngle(now), IdleFollow);
        }

        private void TickAiming(int now)
        {
            var elapsed = now - _stateStartTick;
            var detach = EaseInOut(Mathf.Clamp01(elapsed / (float)DetachTicks));
            var windup = WindupDistance * Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.55f, 1f, AimProgress));

            var hold = _laneStart - _laneDir * windup;
            if (_fireTick - now <= ShakeTicks)
            {
                var perpendicular = new Vector3(_laneDir.z, 0f, -_laneDir.x);
                hold += perpendicular * (ShakeAmplitude * Rand.Range(-1f, 1f));
            }

            _pos = Vector3.Lerp(_moveFrom, hold, detach);
            _angle = LerpAngle(_moveFromAngle, LaneAngle, detach);

            if (elapsed % AimPetalInterval == 0)
            {
                ThrowAimPetals();
            }

            if (now >= _fireTick)
            {
                Fire(now);
            }
        }

        // 조준 중: 요정 비비 사출탄 조준처럼 차오른 띠 위에서 금빛 꽃잎·반짝임이 발사 방향으로 흩날린다.
        private void ThrowAimPetals()
        {
            var map = Map;
            var filled = _laneLength * Mathf.Max(0.15f, AimProgress);
            var mid = PointOnLane(filled * 0.5f);
            if (!mid.ShouldSpawnMotesAt(map)) { return; }

            var perpendicular = new Vector3(_laneDir.z, 0f, -_laneDir.x);
            for (int i = 0; i < 2; i++)
            {
                var pos = PointOnLane(Rand.Range(0f, filled)) + perpendicular * Rand.Range(-HalfWidth, HalfWidth) * 0.85f;
                var def = i == 0 ? ApocryphaFleckDefOf.VV_Fleck_ApocryphaAimSpark : ApocryphaFleckDefOf.VV_Fleck_ApocryphaAimTwinkle;
                var data = FleckMaker.GetDataStatic(pos, map, def, i == 0 ? Rand.Range(0.25f, 0.45f) : Rand.Range(0.2f, 0.35f));
                data.velocityAngle = LaneAngle + Rand.Range(-20f, 20f);
                data.velocitySpeed = Rand.Range(0.5f, 1.3f);
                data.rotation = Rand.Range(0f, 360f);
                data.rotationRate = Rand.Range(-120f, 120f);
                map.flecks.CreateFleck(data);
            }
        }

        // 물러난 자리에서 바로 튀어 나간다. 궤적은 새로 시작한다.
        private void Fire(int now)
        {
            _state = NeedleState.Firing;
            _stateStartTick = now;
            _travelled = NeedleTipOffset - WindupDistance;
            _angle = LaneAngle;
            _hitIds.Clear();
            _ignored = null;

            // 2페이즈: 쏘는 순간 직선 위에 보스가 있으면 아무에게도 피해를 주지 않고 보스에게 꽂힌다.
            _stickAlong = IsUnbound ? BossContactAlong() : -1f;

            _pos = BodyOnLane(_travelled);
            Trail?.ResetTrail();
            RegisterTrail(_pos);

            PlayEffectAt(ApocryphaEffecterDefOf.VV_Effecter_ApocryphaNeedleFire, _pos);
            ApocryphaUtility.ShakeCamera(Map, 0.5f);

            // 쏘는 순간 바늘 몸이 이미 덮고 있는 띠 앞머리(시작점~촉)부터 꿰뚫는다.
            if (_stickAlong < 0f)
            {
                DamageAlong(0f, Mathf.Clamp(_travelled, 0f, _laneLength));
                if (Destroyed) { return; }
            }

            TickFiring(now);
        }

        // _travelled: 촉이 시작점에서 나아간 거리. 몸통(그림 중심)은 촉보다 NeedleTipOffset만큼 뒤에 있다.
        private void TickFiring(int now)
        {
            var from = _travelled;

            // 날아가는 사이에 보스가 직선으로 들어와도 거기서 꽂힌다(이미 지나온 구간의 피해는 그대로).
            if (_stickAlong < 0f && IsUnbound)
            {
                var contact = BossContactAlong();
                if (contact >= 0f && contact <= from + FireSpeed)
                {
                    _stickAlong = contact;
                }
            }

            var end = _stickAlong >= 0f ? Mathf.Max(from, Mathf.Min(_stickAlong, _laneLength)) : _laneLength;
            var to = Mathf.Min(end, _travelled + FireSpeed);

            for (var d = from + TrailPointSpacing; d < to; d += TrailPointSpacing)
            {
                RegisterTrail(BodyOnLane(d));
            }

            _travelled = to;
            _pos = BodyOnLane(to);
            RegisterTrail(_pos);

            if (_stickAlong >= 0f)
            {
                if (to >= end)
                {
                    Stick(now);
                }
                return;
            }

            DamageAlong(Mathf.Max(0f, from), to);
            if (Destroyed) { return; }

            if (to >= _laneLength)
            {
                _state = NeedleState.Lodged;
                _stateStartTick = now;
                PlayEffectAt(ApocryphaEffecterDefOf.VV_Effecter_ApocryphaNeedleEnd, PointOnLane(_laneLength));
            }
        }

        // 사거리 끝에 박혀 잠시 떤다.
        private void TickLodged(int now)
        {
            var elapsed = now - _stateStartTick;
            var shake = ShakeAmplitude * (1f - elapsed / (float)LodgedTicks);
            var perpendicular = new Vector3(_laneDir.z, 0f, -_laneDir.x);
            _pos = BodyOnLane(_laneLength) + perpendicular * (shake * Rand.Range(-1f, 1f));

            if (elapsed >= LodgedTicks)
            {
                StartReturn(now, BodyOnLane(_laneLength), _laneDir);
            }
        }

        // 2페이즈: 보스에게 꽂힌다. 꽂힌 자리(몸통 중심 기준)와 각도를 기억해 보스를 따라 붙어 다닌다.
        private void Stick(int now)
        {
            _state = NeedleState.Stuck;
            _stateStartTick = now;
            _stickAlong = -1f;
            _stuckAngle = LaneAngle;

            var perpendicular = new Vector3(_laneDir.z, 0f, -_laneDir.x);
            _stuckOffset = -_laneDir * StuckDepth + perpendicular * ((_slot - 1) * StuckSpread);
            _pos = BodyCenter + _stuckOffset;
            _angle = _stuckAngle;

            Trail?.FinishTrail();
            PlayEffectAt(ApocryphaEffecterDefOf.VV_Effecter_ApocryphaNeedleStick, BodyCenter);
            ApocryphaUtility.ShakeCamera(Map, 0.7f);

            BossComp?.Notify_NeedleStuck(this);
        }

        // 꽂힌 채 보스를 따라간다. 꽂힌 직후 잠시 떤다.
        private void TickStuck(int now)
        {
            var elapsed = now - _stateStartTick;
            _pos = BodyCenter + _stuckOffset;
            _angle = _stuckAngle;

            if (elapsed < StuckShakeTicks)
            {
                var perpendicular = new Vector3(_laneDir.z, 0f, -_laneDir.x);
                _pos += perpendicular * (ShakeAmplitude * (1f - elapsed / (float)StuckShakeTicks) * Rand.Range(-1f, 1f));
            }
        }

        // from에서 outDir 쪽으로 조금 더 나아갔다가 휘어 자리로 돌아온다.
        private void StartReturn(int now, Vector3 from, Vector3 outDir)
        {
            _state = NeedleState.Returning;
            _stateStartTick = now;
            _returnFrom = from;
            _returnOutDir = outDir;
            _returnProgress = 0f;

            // 베지어 곡선 길이의 근삿값(현과 조절점 다각형 길이의 평균). 일정한 속도로 돌아오는 데 쓴다.
            GetReturnCurve(now, out var p0, out var c1, out var c2, out var p3);
            var chord = Vector3.Distance(p0, p3);
            var polygon = Vector3.Distance(p0, c1) + Vector3.Distance(c1, c2) + Vector3.Distance(c2, p3);
            _returnLength = Mathf.Max(1f, (chord + polygon) * 0.5f);
        }

        private void TickReturning(int now)
        {
            _returnProgress = Mathf.Min(1f, _returnProgress + ReturnSpeed / Mathf.Max(1f, _returnLength));

            GetReturnCurve(now, out var p0, out var c1, out var c2, out var p3);
            var t = _returnProgress;
            _pos = Bezier(p0, c1, c2, p3, t);

            var travel = BezierDerivative(p0, c1, c2, p3, t).Yto0();
            var travelAngle = travel.sqrMagnitude > 0.0001f ? travel.AngleFlat() : _angle;
            _angle = LerpAngle(travelAngle, SlotAngle(now), Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(ReturnBlendStart, 1f, t)));

            RegisterTrail(_pos);

            if (t >= 1f)
            {
                _state = NeedleState.Idle;
                _stateStartTick = now;
                Trail?.FinishTrail();

                if (IsUnbound)
                {
                    _readyTick = now + BossComp.Props.unboundNeedleIntervalTicks.RandomInRange;
                }
            }
        }

        // 귀환 곡선: 출발점에서 _returnOutDir(사거리 끝에서는 발사 방향, 뽑힐 때는 그 반대) 쪽으로 더 나아가다 휘어 들어,
        // 자리 바깥쪽에서 자리로 들어온다(자리는 보스를 따라 움직인다).
        private void GetReturnCurve(int now, out Vector3 p0, out Vector3 c1, out Vector3 c2, out Vector3 p3)
        {
            p0 = _returnFrom;
            p3 = SlotPos(now);

            var outward = (p3 - BodyCenter).Yto0();
            if (outward.sqrMagnitude < 0.0001f)
            {
                outward = Vector3.forward;
            }

            c1 = p0 + _returnOutDir * ReturnOutWeight;
            c2 = p3 + outward.normalized * ReturnInWeight;
        }
        #endregion

        #region 피해
        // 촉이 시작점으로부터 from~to칸 사이를 지나는 동안 경고 띠에 걸친 폰·건물에 한 번씩 관통 피해를 준다(경고 띠와 같은 범위):
        // 폰은 그려지는 자리(DrawPos)를 중심으로 반경 PawnHitRadius가 띠에 걸치면, 건물은 차지한 칸(정사각형)이 띠에 걸치면 맞는다.
        // 띠의 양 끝(시작점·사거리 끝)에서도 같은 만큼 걸치면 맞는다.
        // 보스·거대 말벌·보스가 피운 꽃·엔딩 제단과 영원꽃(사출탄 폭발 제외 목록), 보스 세력, 쓰러진 폰, 세력 없는 건물(자연 암벽·폐허)은 뺀다.
        private void DamageAlong(float from, float to)
        {
            var map = Map;
            var comp = BossComp;
            if (map == null || comp == null || to <= from) { return; }

            if (_ignored == null)
            {
                _ignored = ApocryphaGimmicks.GetBlastIgnoredThings(map, _boss);
            }

            var halfWidth = comp.Props.needleHalfWidth;
            var cellExtent = 0.5f * (Mathf.Abs(_laneDir.x) + Mathf.Abs(_laneDir.z));
            var margin = halfWidth + 2f;
            var a = PointOnLane(from);
            var b = PointOnLane(to);
            var rect = CellRect.FromLimits(
                Mathf.FloorToInt(Mathf.Min(a.x, b.x) - margin),
                Mathf.FloorToInt(Mathf.Min(a.z, b.z) - margin),
                Mathf.CeilToInt(Mathf.Max(a.x, b.x) + margin),
                Mathf.CeilToInt(Mathf.Max(a.z, b.z) + margin)).ClipInsideMap(map);

            foreach (var cell in rect)
            {
                _tmpCellThings.Clear();
                _tmpCellThings.AddRange(cell.GetThingList(map));
                for (int i = 0; i < _tmpCellThings.Count; i++)
                {
                    var thing = _tmpCellThings[i];
                    if (!CanPierce(thing)) { continue; }

                    var hit = thing is Pawn
                        ? OverlapsLane(thing.DrawPos, PawnHitRadius, PawnHitRadius, from, to)
                        : OverlapsLane(cell.ToVector3Shifted(), cellExtent, cellExtent, from, to);
                    if (!hit) { continue; }

                    _hitIds.Add(thing.thingIDNumber);
                    Pierce(thing, comp);
                    if (Destroyed) { break; }
                }

                if (Destroyed) { break; }
            }

            _tmpCellThings.Clear();
        }

        // point를 중심으로 띠 폭 방향 extentAcross, 길이 방향 extentAlong만큼 퍼진 몸이 촉이 지나는 구간(from~to) 안의 띠에 걸치는지.
        // 띠의 양 끝에서는 몸이 걸치기만 해도 들고, 그 사이에서는 몸 중심이 구간 안에 있어야 한다(틱마다 이어지는 구간이 겹쳐 세지 않게).
        private bool OverlapsLane(Vector3 point, float extentAcross, float extentAlong, float from, float to)
        {
            var rel = (point - _laneStart).Yto0();
            var along = rel.x * _laneDir.x + rel.z * _laneDir.z;
            var lo = from <= 0f ? -extentAlong : from;
            var hi = to >= _laneLength ? _laneLength + extentAlong : to;
            if (along < lo || along > hi) { return false; }

            return Mathf.Abs(rel.x * _laneDir.z - rel.z * _laneDir.x) <= HalfWidth + extentAcross;
        }

        private bool CanPierce(Thing thing)
        {
            if (thing.Destroyed || !thing.Spawned) { return false; }

            if (thing is Pawn pawn)
            {
                if (pawn.Dead || pawn.Downed) { return false; }
            }
            else if (thing is Building building)
            {
                if (!building.def.useHitPoints || building.Faction == null) { return false; }
            }
            else
            {
                return false;
            }

            if (_boss != null && thing.Faction != null && thing.Faction == _boss.Faction) { return false; }
            if (_hitIds.Contains(thing.thingIDNumber)) { return false; }

            return !_ignored.Contains(thing);
        }

        private void Pierce(Thing thing, CompApocrypha comp)
        {
            var map = thing.Map;
            var cell = thing.Position;
            var drawPos = thing.DrawPos;

            var dinfo = new DamageInfo(
                ApocryphaDamageDefOf.VV_GiantNeedlePierce,
                comp.Props.needleDamage,
                comp.Props.needleArmorPenetration,
                LaneAngle,
                _boss);
            thing.TakeDamage(dinfo);

            SpawnPierceEffect(map, cell, drawPos);
        }

        // 엘텍스 바늘의 꿰뚫음 연출(VV_Fleck_NeedlePierce + 파편)을 발사 방향으로 크게 낸다.
        private void SpawnPierceEffect(Map map, IntVec3 cell, Vector3 drawPos)
        {
            if (map == null || !cell.InBounds(map)) { return; }

            ApocryphaGimmicks.PlayEffect(ApocryphaEffecterDefOf.VV_Effecter_ApocryphaNeedleHit, cell, map, OffsetFromCell(drawPos, cell));

            var basePos = drawPos.Yto0();
            basePos.y = AltitudeLayer.MoteOverhead.AltitudeFor();
            if (!cell.ShouldSpawnMotesAt(map)) { return; }

            var angle = LaneAngle;
            var pierce = FleckMaker.GetDataStatic(basePos, map, VVFleckDefOf.VV_Fleck_NeedlePierce, Rand.Range(2.2f, 2.6f));
            pierce.rotation = angle;
            map.flecks.CreateFleck(pierce);

            var shardCount = Rand.RangeInclusive(6, 9);
            for (int i = 0; i < shardCount; i++)
            {
                var offset = new Vector3(Rand.Range(-0.2f, 0.2f), 0f, Rand.Range(-0.2f, 0.2f));
                var shard = FleckMaker.GetDataStatic(basePos + offset, map, VVFleckDefOf.VV_Fleck_NeedleShard, Rand.Range(0.45f, 0.8f));
                shard.velocityAngle = angle + Rand.Range(-55f, 55f);
                shard.velocitySpeed = Rand.Range(2f, 4.5f);
                shard.rotation = shard.velocityAngle + Rand.Range(-25f, 25f);
                shard.rotationRate = Rand.Range(-360f, 360f);
                map.flecks.CreateFleck(shard);
            }
        }
        #endregion

        #region 그리기
        protected override void DrawAt(Vector3 drawLoc, bool flip = false)
        {
            var alpha = Mathf.Clamp01((GenTicks.TicksGame - _spawnTick) / (float)FadeInTicks);
            if (alpha <= 0f) { return; }

            // 2페이즈에서 보스가 띠 안에 들어와 있으면 띠를 금빛으로 바꿔 '이대로 쏘면 보스에게 꽂힌다'는 것을 알린다.
            if (_state == NeedleState.Aiming)
            {
                var bossInLane = IsUnbound && BossContactAlong() >= 0f;
                var aimSecs = (GenTicks.TicksGame - _stateStartTick) / 60f;
                ApocryphaNeedleRenderer.DrawLane(_laneStart, _laneDir, _laneLength, HalfWidth * 2f, AimProgress, alpha, bossInLane, aimSecs, _aimTicks / 60f, thingIDNumber);
            }

            // 발사 직후: 촉이 지나간 직선 위의 공간이 순간 일그러진다(_travelled = 촉이 나아간 거리, 꽂혔으면 꽂힌 자리까지).
            var warpAge = GenTicks.TicksGame - _fireTick;
            if ((IsFlying || IsStuck) && warpAge >= 0 && warpAge < WarpTicks)
            {
                ApocryphaNeedleRenderer.DrawWarp(_laneStart, _laneDir, _laneLength, Mathf.Max(0f, _travelled), warpAge / 60f, alpha);
            }

            var glow = _state == NeedleState.Idle || _state == NeedleState.Stuck ? 0f : _state == NeedleState.Aiming ? AimProgress : 1f;
            ApocryphaNeedleRenderer.DrawNeedle(DrawPos, _angle, alpha, glow);
        }
        #endregion

        #region 도우미
        // 보스 몸(발밑과 몸통 중심 두 점, 반경 BossHitRadius)이 발사 직선 띠에 처음 닿는 지점(시작점에서의 거리). 닿지 않으면 -1.
        private float BossContactAlong()
        {
            if (_boss == null || !_boss.Spawned || _laneLength <= 0f) { return -1f; }

            var best = -1f;
            var reach = HalfWidth + BossHitRadius;
            for (int i = 0; i < 2; i++)
            {
                var point = i == 0 ? _boss.DrawPos.Yto0() : BodyCenter;
                var rel = (point - _laneStart).Yto0();
                var along = rel.x * _laneDir.x + rel.z * _laneDir.z;
                if (along < -BossHitRadius || along > _laneLength + BossHitRadius) { continue; }
                if (Mathf.Abs(rel.x * _laneDir.z - rel.z * _laneDir.x) > reach) { continue; }

                var contact = Mathf.Clamp(along - BossHitRadius, 0f, _laneLength);
                if (best < 0f || contact < best)
                {
                    best = contact;
                }
            }

            return best;
        }

        // 자리 위치: 1페이즈는 등 뒤 핀 판넬의 slot번째 바늘(안쪽부터), 2페이즈는 몸통 중심 둘레 궤도를 고르게 나눈 slot번째 자리.
        private Vector3 SlotPos(int now)
        {
            if (IsUnbound)
            {
                var bob = SlotBobHeight * Mathf.Sin(now * 0.05f + _slot * 1.9f);
                var orbit = OrbitDegrees(now) * Mathf.Deg2Rad;
                return BodyCenter + new Vector3(Mathf.Cos(orbit) * OrbitRadiusX, 0f, Mathf.Sin(orbit) * OrbitRadiusZ + bob);
            }

            // 판넬 전체가 한 몸처럼 함께 오르내린다.
            var side = FinSide;
            var root = new Vector3((FinRootX + _slot * FinStepX) * side, 0f, FinRootZ + _slot * FinStepZ + FinBobHeight * Mathf.Sin(now * 0.04f));
            return BodyCenter + root + FinDirection(now, side) * FinCenterFromRoot;
        }

        // 핀 판넬 바늘이 가리키는 방향(위에서 바깥쪽으로 기움).
        private Vector3 FinDirection(int now, float side)
        {
            var tilt = (FinTiltInner + _slot * FinTiltStep + FinSwayDegrees * Mathf.Sin(now * 0.02f + _slot * 0.9f)) * Mathf.Deg2Rad;
            return new Vector3(Mathf.Sin(tilt) * side, 0f, Mathf.Cos(tilt));
        }

        private float OrbitDegrees(int now)
        {
            var count = Mathf.Max(1, BossComp?.NeedleCount ?? 1);
            return _slot * 360f / count + (now % 1440) * OrbitDegreesPerTick;
        }

        // 자리에서의 바늘 끝 방향: 1페이즈는 핀 판넬 방향, 2페이즈는 몸통 중심의 바깥쪽.
        private float SlotAngle(int now)
        {
            if (!IsUnbound)
            {
                return FinDirection(now, FinSide).AngleFlat();
            }

            var orbit = OrbitDegrees(now) * Mathf.Deg2Rad;
            return new Vector3(Mathf.Cos(orbit), 0f, Mathf.Sin(orbit)).AngleFlat();
        }

        private Vector3 PointOnLane(float distance)
        {
            return _laneStart + _laneDir * distance;
        }

        // 촉이 tipDistance에 있을 때 바늘 몸통(그림 중심)의 자리
        private Vector3 BodyOnLane(float tipDistance)
        {
            return PointOnLane(tipDistance - NeedleTipOffset);
        }

        // 맵 밖으로 나가지 않는 발사 직선 길이(최대 range).
        private float LaneLengthInsideMap(Vector3 start, Vector3 direction, float range)
        {
            var size = Map.Size;
            var length = range;
            length = Mathf.Min(length, DistanceToBound(start.x, direction.x, size.x));
            length = Mathf.Min(length, DistanceToBound(start.z, direction.z, size.z));
            return Mathf.Max(0f, length);
        }

        private static float DistanceToBound(float start, float direction, int size)
        {
            if (direction > 0.0001f) { return (size - MapEdgeMargin - start) / direction; }
            if (direction < -0.0001f) { return (MapEdgeMargin - start) / direction; }

            return float.MaxValue;
        }

        private Vector3 ClampInsideMap(Vector3 point)
        {
            var size = Map.Size;
            point.x = Mathf.Clamp(point.x, MapEdgeMargin, size.x - MapEdgeMargin);
            point.z = Mathf.Clamp(point.z, MapEdgeMargin, size.z - MapEdgeMargin);
            return point;
        }

        private void SyncPosition()
        {
            var cell = _pos.ToIntVec3().ClampInsideMap(Map);
            if (cell != Position)
            {
                Position = cell;
            }
        }

        private void RegisterTrail(Vector3 point)
        {
            point.y = AltitudeLayer.MoteOverhead.AltitudeFor() - 0.01f;
            Trail?.RegisterNewTrail(point);
        }

        // 정확한 월드 위치에서 이펙트를 한 번 재생한다(칸 중심에서의 차이를 이펙터 오프셋으로 넘긴다).
        private void PlayEffectAt(EffecterDef effecterDef, Vector3 point)
        {
            var cell = point.ToIntVec3().ClampInsideMap(Map);
            ApocryphaGimmicks.PlayEffect(effecterDef, cell, Map, OffsetFromCell(point, cell));
        }

        private static Vector3 OffsetFromCell(Vector3 point, IntVec3 cell)
        {
            return (point - cell.ToVector3Shifted()).WithY(0f);
        }

        private static float EaseInOut(float t)
        {
            return t * t * (3f - 2f * t);
        }

        // 1.7 Mathf에는 LerpAngle이 없다. 짧은 쪽으로 돌아가며 보간한다.
        private static float LerpAngle(float from, float to, float t)
        {
            return from + Mathf.DeltaAngle(from, to) * Mathf.Clamp01(t);
        }

        private static Vector3 Bezier(Vector3 p0, Vector3 c1, Vector3 c2, Vector3 p3, float t)
        {
            var u = 1f - t;
            return u * u * u * p0 + 3f * u * u * t * c1 + 3f * u * t * t * c2 + t * t * t * p3;
        }

        private static Vector3 BezierDerivative(Vector3 p0, Vector3 c1, Vector3 c2, Vector3 p3, float t)
        {
            var u = 1f - t;
            return 3f * u * u * (c1 - p0) + 6f * u * t * (c2 - c1) + 3f * t * t * (p3 - c2);
        }
        #endregion
    }
}
