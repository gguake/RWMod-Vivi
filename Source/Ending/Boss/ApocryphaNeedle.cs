using RimWorld;
using RPEF;
using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace VVRace
{
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
            Planted = 6,
            LockOn = 7,
            Scattering = 8,
        }

        private const float FinRootX = 0.46f;
        private const float FinRootZ = 0.41f;
        private const float FinStepX = 0.32f;
        private const float FinStepZ = -0.14f;
        private const float FinTiltInner = 16f;
        private const float FinTiltStep = 11f;
        private const float FinCenterFromRoot = 2f;
        private const float FinSwayDegrees = 2.5f;
        private const float FinBobHeight = 0.08f;
        private const float FinBehindAltitude = 0.03f;
        private const float FinRackReturnProgress = 0.85f;
        private const float SlotBobHeight = 0.14f;
        private const float IdleFollow = 0.16f;

        private const float OrbitRadiusX = 5.4f;
        private const float OrbitRadiusZ = 4.4f;
        private const float OrbitDegreesPerTick = 0.25f;

        private const float BossHitRadius = 1.6f;
        private const float StuckDepth = 1.4f;
        private const float StuckSpread = 0.45f;
        private const int StuckShakeTicks = 14;

        private const float LaunchOffset = 3.2f;
        private const float CloseTargetMargin = 2f;
        private const float CloseTargetFanAngle = 75f;
        private const int DetachTicks = 24;
        private const float WindupDistance = 0.8f;
        private const int ShakeTicks = 24;
        private const float ShakeAmplitude = 0.08f;

        private const float FireSpeed = 7f;
        private const int LodgedTicks = 16;

        private const float ReturnSpeed = 0.9f;
        private const float ReturnOutWeight = 7f;
        private const float ReturnInWeight = 4f;
        private const float ReturnBlendStart = 0.75f;

        private const int AimPetalInterval = 4;

        private const float ScatterSpeed = 0.55f;
        private const int ScatterMinTicks = 24;
        private const int ScatterMaxTicks = 50;
        private const float ScatterOutWeight = 3f;
        private const float ScatterInWeight = 2.5f;
        private const float ScatterLift = 1.6f;

        private const int LiftTicks = 30;
        private const int PlantedGlintInterval = 45;
        private const float ClickRadius = 1.4f;

        // 발사 왜곡의 재생 시간. VV_NeedleWarp의 _Duration(0.55초)과 맞춘다.
        private const int WarpTicks = 33;

        private const int FadeInTicks = 40;

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
        private bool _breach;
        private Vector3 _returnFrom;
        private float _returnLength;
        private float _returnProgress;
        private Vector3 _returnOutDir;
        private List<int> _hitIds = new List<int>();
        private float _stickAlong = -1f;
        private Vector3 _stuckOffset;
        private float _stuckAngle;
        private Pawn _lockTarget;
        private int _activateTick;
        private int _lockEndTick;
        private IntVec3 _groundCell = IntVec3.Invalid;
        private Vector3 _plantTip;
        private Vector3 _scatterFrom;
        private Vector3 _scatterTo;
        private Vector3 _scatterOut;
        private float _scatterAngle;
        private int _scatterTicks;

        private int _spawnTick;
        private CompApocrypha _bossComp;
        private CompTrailRenderer _trail;
        private List<Thing> _ignored;

        public int Slot => _slot;
        public bool IsIdle => _state == NeedleState.Idle;
        public bool IsAiming => _state == NeedleState.Aiming;
        public bool IsFlying => _state == NeedleState.Firing || _state == NeedleState.Lodged || _state == NeedleState.Returning;
        public bool IsStuck => _state == NeedleState.Stuck;
        public bool IsPlanted => _state == NeedleState.Planted;
        public bool IsScattering => _state == NeedleState.Scattering;

        public bool CanActivate => Spawned && IsPlanted && IsUnbound;

        public int ActivateTicks => BossComp?.Props.unboundNeedleActivateTicks ?? 60;

        public IntVec3 GroundCell => _groundCell;

        public Pawn LockTarget => _state == NeedleState.LockOn || (_state == NeedleState.Aiming && IsUnbound) ? _lockTarget : null;

        public float AimProgress => IsAiming ? Mathf.Clamp01((GenTicks.TicksGame - _stateStartTick) / (float)Mathf.Max(1, _aimTicks)) : 0f;

        public Vector3 LaneStart => _laneStart;
        public Vector3 LaneEnd => _laneStart + _laneDir * _laneLength;
        public float LaneLength => _laneLength;

        public override Vector3 DrawPos => new Vector3(_pos.x, DrawAltitude, _pos.z);

        private bool IsRacked => !IsUnbound && (_state == NeedleState.Idle || (_state == NeedleState.Returning && _returnProgress >= FinRackReturnProgress));

        // 등 뒤 바늘이 몸에 가려지도록 보스보다 낮게 그린다. 보스가 등을 보이거나 바늘이 비행 중이면 높게 그린다.
        private float DrawAltitude
        {
            get
            {
                if (_state == NeedleState.Planted) { return AltitudeLayer.BuildingOnTop.AltitudeFor(); }

                return IsRacked && FinBehindBody ? AltitudeLayer.Pawn.AltitudeFor() - FinBehindAltitude : AltitudeLayer.MoteOverhead.AltitudeFor();
            }
        }

        // 보스의 왼쪽 어깨는 정면·서향에서 화면 오른쪽(+1), 후면·동향에서 화면 왼쪽(-1)에 해당한다.
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

            _spawnTick = GenTicks.TicksGame;

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
            Scribe_Values.Look(ref _breach, "breach", false);
            Scribe_Values.Look(ref _returnFrom, "returnFrom");
            Scribe_Values.Look(ref _returnLength, "returnLength", 0f);
            Scribe_Values.Look(ref _returnProgress, "returnProgress", 0f);
            Scribe_Values.Look(ref _returnOutDir, "returnOutDir");
            Scribe_Collections.Look(ref _hitIds, "hitIds", LookMode.Value);
            Scribe_Values.Look(ref _stickAlong, "stickAlong", -1f);
            Scribe_Values.Look(ref _stuckOffset, "stuckOffset");
            Scribe_Values.Look(ref _stuckAngle, "stuckAngle", 0f);
            Scribe_References.Look(ref _lockTarget, "lockTarget");
            Scribe_Values.Look(ref _activateTick, "activateTick", 0);
            Scribe_Values.Look(ref _lockEndTick, "lockEndTick", 0);
            Scribe_Values.Look(ref _groundCell, "groundCell", IntVec3.Invalid);
            Scribe_Values.Look(ref _plantTip, "plantTip");
            Scribe_Values.Look(ref _scatterFrom, "scatterFrom");
            Scribe_Values.Look(ref _scatterTo, "scatterTo");
            Scribe_Values.Look(ref _scatterOut, "scatterOut");
            Scribe_Values.Look(ref _scatterAngle, "scatterAngle", 0f);
            Scribe_Values.Look(ref _scatterTicks, "scatterTicks", 0);

            if (Scribe.mode == LoadSaveMode.PostLoadInit && _hitIds == null)
            {
                _hitIds = new List<int>();
            }
        }

        #region 보스가 부르는 API
        public void StartAim(IntVec3 targetCell, int aimTicks, float? fanAngle = null, bool breach = false)
        {
            if (!Spawned || _boss == null || !_boss.Spawned) { return; }

            _breach = breach;

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

            // 대상이 발사 지점보다 충분히 앞에 있지 않으면 경고 띠가 대상을 덮지 못하므로 몸통 옆 부채꼴 위치에서 겨눈다.
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
            _laneStart = ApocryphaNeedleLane.ClampInsideMap(Map, start);
            _laneLength = ApocryphaNeedleLane.LengthInsideMap(Map, _laneStart, direction, BossComp?.Props.needleRange ?? 32f);

            _moveFrom = _pos;
            _moveFromAngle = _angle;
            _aimTicks = Mathf.Max(1, aimTicks);
            _fireTick = now + _aimTicks;
            _state = NeedleState.Aiming;
            _stateStartTick = now;

            PlayEffectAt(ApocryphaEffecterDefOf.VV_Effecter_ApocryphaNeedleAim, _pos);
        }

        // 2페이즈는 폰이 바늘을 깨워 조준하므로 보스가 기절해도 취소하지 않는다.
        public bool CancelAim()
        {
            if (_state != NeedleState.Aiming || IsUnbound) { return false; }

            _state = NeedleState.Idle;
            _stateStartTick = GenTicks.TicksGame;
            return true;
        }

        public bool PullOut(IntVec3 cell)
        {
            if (_state != NeedleState.Stuck || !Spawned) { return false; }

            PlayEffectAt(ApocryphaEffecterDefOf.VV_Effecter_ApocryphaNeedlePullOut, _pos);
            ScatterTo(cell, -_laneDir);
            return true;
        }

        public void ScatterTo(IntVec3 cell, Vector3 outDir)
        {
            if (!Spawned || !cell.IsValid) { return; }

            var now = GenTicks.TicksGame;
            var tip = cell.ToVector3Shifted().Yto0();
            var from = _pos.Yto0();
            var travel = (tip - from).Yto0();
            var direction = travel.sqrMagnitude > 0.01f ? travel.normalized : (outDir.sqrMagnitude > 0.01f ? outDir.normalized : Vector3.forward);

            _groundCell = cell;
            _plantTip = tip;
            _scatterFrom = _pos;
            _scatterOut = outDir.sqrMagnitude > 0.01f ? outDir.Yto0().normalized : direction;
            _scatterAngle = direction.AngleFlat();
            _scatterTo = tip - direction * ApocryphaNeedleLane.TipOffset;
            _scatterTicks = Mathf.Clamp(Mathf.RoundToInt(travel.magnitude / ScatterSpeed), ScatterMinTicks, ScatterMaxTicks);
            _lockTarget = null;
            _state = NeedleState.Scattering;
            _stateStartTick = now;

            Trail?.ResetTrail();
            RegisterTrail(_pos);
        }

        public void Activate(Pawn activator)
        {
            if (!CanActivate || activator == null) { return; }

            var now = GenTicks.TicksGame;
            var props = BossComp.Props;

            _lockTarget = activator;
            _breach = false;
            _laneStart = ApocryphaNeedleLane.ClampInsideMap(Map, _plantTip);
            _laneDir = (_pos - _plantTip).Yto0().sqrMagnitude > 0.01f ? -(_pos - _plantTip).Yto0().normalized : Vector3.forward;
            UpdateLockDirection();

            _moveFrom = _pos;
            _moveFromAngle = _angle;
            _activateTick = now;
            _lockEndTick = now + Mathf.Max(1, props.unboundNeedleLockOnTicks);
            _state = NeedleState.LockOn;
            _stateStartTick = now;

            PlayEffectAt(ApocryphaEffecterDefOf.VV_Effecter_ApocryphaNeedleAim, _plantTip);
            Messages.Message(LocalizeString_EndingBoss.VV_Message_ApocryphaNeedleActivated.Translate(activator.Named("PAWN")), this, MessageTypeDefOf.CautionInput, historical: false);
        }

        public static ApocryphaNeedle FindPlantedNear(Map map, Vector3 clickPos)
        {
            var def = ApocryphaThingDefOf.VV_ApocryphaNeedle;
            if (map == null || def == null) { return null; }

            ApocryphaNeedle best = null;
            var bestDistance = ClickRadius;
            var things = map.listerThings.ThingsOfDef(def);
            for (int i = 0; i < things.Count; i++)
            {
                if (!(things[i] is ApocryphaNeedle needle) || !needle.IsPlanted) { continue; }

                var distance = Mathf.Min((needle._plantTip - clickPos).MagnitudeHorizontal(), (needle._pos - clickPos).MagnitudeHorizontal());
                if (distance < bestDistance)
                {
                    best = needle;
                    bestDistance = distance;
                }
            }

            return best;
        }

        public void DebugStick()
        {
            if (!Spawned || !IsUnbound || _state == NeedleState.Stuck || IsFlying) { return; }

            var direction = (BodyCenter - _pos.Yto0()).Yto0();
            _laneDir = direction.sqrMagnitude > 0.01f ? direction.normalized : Vector3.forward;
            Stick(GenTicks.TicksGame);
        }

        public void Vanish()
        {
            if (Destroyed) { return; }

            if (Spawned)
            {
                PlayEffectAt(ApocryphaEffecterDefOf.VV_Effecter_ApocryphaNeedleVanish, _pos);
            }

            Destroy(DestroyMode.Vanish);
        }

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
            if (comp == null || !_boss.Spawned || _boss.Map != Map
                || (comp.Phase != ApocryphaPhase.Bound && comp.Phase != ApocryphaPhase.Unbound && comp.Phase != ApocryphaPhase.Transforming))
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

                case NeedleState.Planted:
                    TickPlanted(now);
                    break;

                case NeedleState.LockOn:
                    TickLockOn(now);
                    break;

                case NeedleState.Scattering:
                    TickScattering(now);
                    break;
            }

            if (Spawned)
            {
                SyncPosition();
            }
        }

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

        private void ThrowAimPetals()
        {
            ApocryphaNeedleLane.ThrowAimPetals(Map, _laneStart, _laneDir, _laneLength, HalfWidth, AimProgress);
        }

        private void Fire(int now)
        {
            _state = NeedleState.Firing;
            _stateStartTick = now;
            _travelled = ApocryphaNeedleLane.TipOffset - WindupDistance;
            _angle = LaneAngle;
            _hitIds.Clear();
            _ignored = null;

            _stickAlong = IsUnbound ? BossContactAlong() : -1f;

            _pos = BodyOnLane(_travelled);
            Trail?.ResetTrail();
            RegisterTrail(_pos);

            PlayEffectAt(ApocryphaEffecterDefOf.VV_Effecter_ApocryphaNeedleFire, _pos);
            ApocryphaUtility.ShakeCamera(Map, 0.5f);

            if (_stickAlong < 0f)
            {
                DamageAlong(0f, Mathf.Clamp(_travelled, 0f, _laneLength));
                if (Destroyed) { return; }
            }

            TickFiring(now);
        }

        // _travelled는 시작점에서 촉까지의 거리다. 그림 중심은 촉보다 TipOffset만큼 뒤에 있다.
        private void TickFiring(int now)
        {
            var from = _travelled;

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

            ApocryphaNeedleLane.RegisterTrailAlong(Trail, _laneStart, _laneDir, from, to);
            _travelled = to;
            _pos = BodyOnLane(to);

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

        private void TickLodged(int now)
        {
            var elapsed = now - _stateStartTick;
            var shake = ShakeAmplitude * (1f - elapsed / (float)LodgedTicks);
            var perpendicular = new Vector3(_laneDir.z, 0f, -_laneDir.x);
            _pos = BodyOnLane(_laneLength) + perpendicular * (shake * Rand.Range(-1f, 1f));

            if (elapsed >= LodgedTicks)
            {
                if (IsUnbound)
                {
                    PlantAtLaneEnd(now);
                }
                else
                {
                    StartReturn(now, BodyOnLane(_laneLength), _laneDir);
                }
            }
        }

        private void PlantAtLaneEnd(int now)
        {
            var map = Map;
            var tip = PointOnLane(_laneLength);
            var cell = tip.ToIntVec3().ClampInsideMap(map);
            if (cell.Standable(map))
            {
                _groundCell = cell;
                _plantTip = tip.Yto0();
                _scatterTo = BodyOnLane(_laneLength);
                _scatterAngle = LaneAngle;
                Plant(now);
                return;
            }

            for (var d = _laneLength - 1f; d > 0f; d -= 1f)
            {
                var back = PointOnLane(d).ToIntVec3();
                if (back.InBounds(map) && back.Standable(map))
                {
                    ScatterTo(back, -_laneDir);
                    return;
                }
            }

            var near = CellFinder.StandableCellNear(cell, map, 6f);
            ScatterTo(near.IsValid ? near : _boss.Position, -_laneDir);
        }

        private void Plant(int now)
        {
            _state = NeedleState.Planted;
            _stateStartTick = now;
            _pos = _scatterTo;
            _angle = _scatterAngle;
            _lockTarget = null;

            Trail?.FinishTrail();
            PlayEffectAt(ApocryphaEffecterDefOf.VV_Effecter_ApocryphaNeedlePlant, _plantTip);
            ApocryphaUtility.ShakeCamera(Map, 0.3f);
        }

        private void TickScattering(int now)
        {
            var t = Mathf.Clamp01((now - _stateStartTick) / (float)Mathf.Max(1, _scatterTicks));
            var eased = t * t * (3f - 2f * t);

            GetScatterCurve(out var p0, out var c1, out var c2, out var p3);
            _pos = Bezier(p0, c1, c2, p3, eased);

            var travel = BezierDerivative(p0, c1, c2, p3, eased).Yto0();
            var travelAngle = travel.sqrMagnitude > 0.0001f ? travel.AngleFlat() : _angle;
            _angle = LerpAngle(travelAngle, _scatterAngle, Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.7f, 1f, t)));

            RegisterTrail(_pos);

            if (t >= 1f)
            {
                Plant(now);
            }
        }

        private void GetScatterCurve(out Vector3 p0, out Vector3 c1, out Vector3 c2, out Vector3 p3)
        {
            p0 = _scatterFrom;
            p3 = _scatterTo;

            var inDir = (_plantTip - _scatterTo).Yto0();
            inDir = inDir.sqrMagnitude > 0.0001f ? inDir.normalized : Vector3.forward;

            c1 = p0 + _scatterOut * ScatterOutWeight + new Vector3(0f, 0f, ScatterLift);
            c2 = p3 - inDir * ScatterInWeight + new Vector3(0f, 0f, ScatterLift * 0.5f);
        }

        private void TickPlanted(int now)
        {
            if ((now + thingIDNumber) % PlantedGlintInterval != 0) { return; }

            var map = Map;
            if (!_plantTip.ShouldSpawnMotesAt(map)) { return; }

            var data = FleckMaker.GetDataStatic(_plantTip + new Vector3(Rand.Range(-0.4f, 0.4f), 0f, Rand.Range(-0.2f, 0.5f)), map, ApocryphaFleckDefOf.VV_Fleck_ApocryphaAimTwinkle, Rand.Range(0.3f, 0.5f));
            data.velocityAngle = Rand.Range(-30f, 30f);
            data.velocitySpeed = Rand.Range(0.2f, 0.6f);
            data.rotation = Rand.Range(0f, 360f);
            map.flecks.CreateFleck(data);
        }

        private void TickLockOn(int now)
        {
            UpdateLockDirection();

            var elapsed = now - _stateStartTick;
            var lift = EaseInOut(Mathf.Clamp01(elapsed / (float)LiftTicks));
            var hold = _laneStart - _laneDir * (WindupDistance * 0.5f);
            _pos = Vector3.Lerp(_moveFrom, hold, lift);
            _angle = LerpAngle(_moveFromAngle, LaneAngle, lift);

            if (elapsed % AimPetalInterval == 0)
            {
                ThrowAimPetals();
            }

            if (now < _lockEndTick) { return; }

            _moveFrom = _pos;
            _moveFromAngle = _angle;
            _aimTicks = Mathf.Max(1, BossComp?.Props.unboundNeedleLockedTicks ?? 75);
            _fireTick = now + _aimTicks;
            _state = NeedleState.Aiming;
            _stateStartTick = now;

            PlayEffectAt(ApocryphaEffecterDefOf.VV_Effecter_ApocryphaNeedleAim, _pos);
        }

        private void UpdateLockDirection()
        {
            var target = _lockTarget;
            if (target != null && target.Spawned && target.Map == Map)
            {
                var toTarget = (target.DrawPos - _laneStart).Yto0();
                if (toTarget.sqrMagnitude > 0.04f)
                {
                    _laneDir = toTarget.normalized;
                }
            }

            if (_laneDir.sqrMagnitude < 0.01f)
            {
                _laneDir = Vector3.forward;
            }

            _laneLength = ApocryphaNeedleLane.LengthInsideMap(Map, _laneStart, _laneDir, BossComp?.Props.needleRange ?? 32f);
        }

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

        private void StartReturn(int now, Vector3 from, Vector3 outDir)
        {
            _state = NeedleState.Returning;
            _stateStartTick = now;
            _returnFrom = from;
            _returnOutDir = outDir;
            _returnProgress = 0f;

            // 귀환 속도를 일정하게 맞추기 위해 현 길이와 제어점 다각형 길이의 평균으로 베지어 곡선 길이를 근사한다.
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
            }
        }

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
        private void DamageAlong(float from, float to)
        {
            var map = Map;
            var comp = BossComp;
            if (map == null || comp == null || to <= from) { return; }

            if (_ignored == null)
            {
                _ignored = ApocryphaGimmicks.GetBlastIgnoredThings(map, _boss);
            }

            ApocryphaNeedleLane.PierceAlong(this, _laneStart, _laneDir, _laneLength, comp.Props.needleHalfWidth, from, to, _hitIds, CanPierce, thing => Pierce(thing, comp));
        }

        private bool CanPierce(Thing thing)
        {
            if (_boss != null && thing.Faction != null && thing.Faction == _boss.Faction) { return false; }

            return !_ignored.Contains(thing);
        }

        private void Pierce(Thing thing, CompApocrypha comp)
        {
            var damage = comp.Props.needleDamage;
            if (_breach && ApocryphaUtility.BlocksPath(thing))
            {
                damage *= comp.Props.needleBreachDamageFactor;
            }

            ApocryphaNeedleLane.Pierce(thing, damage, comp.Props.needleArmorPenetration, LaneAngle, _boss);
        }
        #endregion

        #region 그리기
        protected override void DrawAt(Vector3 drawLoc, bool flip = false)
        {
            var alpha = Mathf.Clamp01((GenTicks.TicksGame - _spawnTick) / (float)FadeInTicks);
            if (alpha <= 0f) { return; }

            if (_state == NeedleState.Aiming || _state == NeedleState.LockOn)
            {
                var unbound = IsUnbound;
                var bossInLane = unbound && BossContactAlong() >= 0f;
                float progress;
                float aimSecs;
                float aimDuration;
                if (unbound)
                {
                    var lockTicks = Mathf.Max(1, _lockEndTick - _activateTick);
                    progress = _state == NeedleState.LockOn
                        ? 0.5f * Mathf.Clamp01((GenTicks.TicksGame - _activateTick) / (float)lockTicks)
                        : 0.5f + 0.5f * AimProgress;
                    aimSecs = (GenTicks.TicksGame - _activateTick) / 60f;
                    aimDuration = (lockTicks + (BossComp?.Props.unboundNeedleLockedTicks ?? 75)) / 60f;
                }
                else
                {
                    progress = AimProgress;
                    aimSecs = (GenTicks.TicksGame - _stateStartTick) / 60f;
                    aimDuration = _aimTicks / 60f;
                }

                ApocryphaNeedleRenderer.DrawLane(_laneStart, _laneDir, _laneLength, HalfWidth * 2f, progress, alpha, bossInLane, aimSecs, aimDuration, thingIDNumber);
            }

            var warpAge = GenTicks.TicksGame - _fireTick;
            if ((IsFlying || IsStuck) && warpAge >= 0 && warpAge < WarpTicks)
            {
                ApocryphaNeedleRenderer.DrawWarp(_laneStart, _laneDir, _laneLength, Mathf.Max(0f, _travelled), warpAge / 60f, alpha);
            }

            if (IsStuck || IsPlanted)
            {
                var stuckSecs = (GenTicks.TicksGame - _stateStartTick) / 60f;
                if (IsPlanted)
                {
                    ApocryphaNeedleRenderer.DrawPlantedMarker(_plantTip, stuckSecs, alpha, thingIDNumber);
                }

                ApocryphaNeedleRenderer.DrawStuckNeedle(DrawPos, _angle, alpha, stuckSecs);
                return;
            }

            var glow = _state == NeedleState.Idle ? 0f
                : _state == NeedleState.Aiming ? AimProgress
                : _state == NeedleState.LockOn ? 0.5f * Mathf.Clamp01((GenTicks.TicksGame - _activateTick) / (float)Mathf.Max(1, _lockEndTick - _activateTick))
                : 1f;
            ApocryphaNeedleRenderer.DrawNeedle(DrawPos, _angle, alpha, glow);
        }
        #endregion

        #region 도우미
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

        private Vector3 SlotPos(int now)
        {
            if (IsUnbound)
            {
                var bob = SlotBobHeight * Mathf.Sin(now * 0.05f + _slot * 1.9f);
                var orbit = OrbitDegrees(now) * Mathf.Deg2Rad;
                return BodyCenter + new Vector3(Mathf.Cos(orbit) * OrbitRadiusX, 0f, Mathf.Sin(orbit) * OrbitRadiusZ + bob);
            }

            var side = FinSide;
            var root = new Vector3((FinRootX + _slot * FinStepX) * side, 0f, FinRootZ + _slot * FinStepZ + FinBobHeight * Mathf.Sin(now * 0.04f));
            return BodyCenter + root + FinDirection(now, side) * FinCenterFromRoot;
        }

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

        private Vector3 BodyOnLane(float tipDistance)
        {
            return ApocryphaNeedleLane.BodyOnLane(_laneStart, _laneDir, tipDistance);
        }

        private void SyncPosition()
        {
            var cell = (_state == NeedleState.Planted || _state == NeedleState.LockOn) && _groundCell.IsValid
                ? _groundCell
                : _pos.ToIntVec3().ClampInsideMap(Map);
            if (cell != Position)
            {
                Position = cell;
            }
        }

        private void RegisterTrail(Vector3 point)
        {
            ApocryphaNeedleLane.RegisterTrail(Trail, point);
        }

        private void PlayEffectAt(EffecterDef effecterDef, Vector3 point)
        {
            ApocryphaNeedleLane.PlayEffectAt(Map, effecterDef, point);
        }

        private static float EaseInOut(float t)
        {
            return t * t * (3f - 2f * t);
        }

        // 1.7 Mathf에는 LerpAngle이 없으므로 짧은 회전 방향으로 직접 보간한다.
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
