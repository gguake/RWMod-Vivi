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

        private const float HaloLift = 0.9f;
        private const float HaloRootRadius = 0.55f;
        private const float HaloCenterFromRoot = 2f;
        private const float HaloSpreadDegrees = 72f;
        private const float HaloSwayDegrees = 2.5f;
        private const float HaloBobHeight = 0.08f;
        private const float HaloBehindAltitude = 0.03f;
        private const float HaloReturnProgress = 0.85f;
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
        private const float ReturnOutWeight = 6f;
        private const float ReturnInWeight = 4f;
        private const float ReturnArcFactor = 0.45f;
        private const float ReturnArcMin = 5f;
        private const float ReturnArcMax = 14f;
        private const float ReturnStartSideRatio = 0.45f;
        private const float ReturnStartBlendEnd = 0.16f;
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
        private int _fireTick = -1;
        private float _travelled;
        private Vector3 _returnFrom;
        private float _returnLength;
        private float _returnProgress;
        private Vector3 _returnOutDir;
        private float _returnSide = 1f;
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

        public bool CanActivate => Spawned && IsPlanted && IsQueen;

        public int ActivateTicks => BossComp?.Props.needleActivateTicks ?? 60;

        public IntVec3 GroundCell => _groundCell;

        public Pawn LockTarget => _state == NeedleState.LockOn || (_state == NeedleState.Aiming && IsQueen) ? _lockTarget : null;

        public float AimProgress => IsAiming ? Mathf.Clamp01((GenTicks.TicksGame - _stateStartTick) / (float)Mathf.Max(1, _aimTicks)) : 0f;

        public override Vector3 DrawPos => new Vector3(_pos.x, DrawAltitude, _pos.z);

        private bool IsRacked => !IsQueen && (_state == NeedleState.Idle || (_state == NeedleState.Returning && _returnProgress >= HaloReturnProgress));

        private float DrawAltitude
        {
            get
            {
                if (_state == NeedleState.Planted) { return AltitudeLayer.BuildingOnTop.AltitudeFor(); }

                return IsRacked && HaloBehindBody ? AltitudeLayer.Pawn.AltitudeFor() - HaloBehindAltitude : AltitudeLayer.MoteOverhead.AltitudeFor();
            }
        }

        private bool HaloBehindBody => _boss != null && _boss.Rotation != Rot4.North;

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

        private bool IsQueen => BossComp?.Phase == ApocryphaPhase.Queen;

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
            Scribe_Values.Look(ref _fireTick, "fireTick", -1);
            Scribe_Values.Look(ref _travelled, "travelled", 0f);
            Scribe_Values.Look(ref _returnFrom, "returnFrom");
            Scribe_Values.Look(ref _returnLength, "returnLength", 0f);
            Scribe_Values.Look(ref _returnProgress, "returnProgress", 0f);
            Scribe_Values.Look(ref _returnOutDir, "returnOutDir");
            Scribe_Values.Look(ref _returnSide, "returnSide", 1f);
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
        }

        #region 보스가 부르는 API
        public void StartAim(IntVec3 targetCell, int aimTicks, float? fanAngle = null)
        {
            if (!Spawned || _boss == null || !_boss.Spawned) { return; }

            var origin = BodyCenter;
            var target = targetCell.ToVector3Shifted().Yto0();
            var direction = EndingVfxUtility.SafeNormalizeHorizontal(target - origin, EndingVfxUtility.SafeNormalizeHorizontal(_pos - origin, Vector3.forward));

            var start = origin + direction * LaunchOffset;
            fanAngle ??= ResolveCloseTargetFanAngle(origin, target, direction);
            if (fanAngle.HasValue)
            {
                var radius = BossComp?.Props.needleFanRadius ?? 5f;
                start = origin + Quaternion.AngleAxis(fanAngle.Value, Vector3.up) * direction * radius;
                direction = EndingVfxUtility.SafeNormalizeHorizontal(target - start, direction);
            }

            BeginAim(start, direction, aimTicks);
        }

        public void StartAimDirection(Vector3 direction, int aimTicks)
        {
            if (!Spawned || _boss == null || !_boss.Spawned) { return; }

            direction = EndingVfxUtility.SafeNormalizeHorizontal(direction, Vector3.forward);
            BeginAim(BodyCenter + direction * LaunchOffset, direction, aimTicks);
        }

        private void BeginAim(Vector3 start, Vector3 direction, int aimTicks)
        {
            var now = GenTicks.TicksGame;
            _laneDir = direction;
            _laneStart = ApocryphaNeedleLane.ClampInsideMap(Map, start);
            _laneLength = ApocryphaNeedleLane.LengthInsideMap(Map, _laneStart, direction, BossComp?.Props.needleRange ?? 19f);

            _moveFrom = _pos;
            _moveFromAngle = _angle;
            _aimTicks = Mathf.Max(1, aimTicks);
            _fireTick = now + _aimTicks;
            _state = NeedleState.Aiming;
            _stateStartTick = now;

            PlayEffectAt(VVEffecterDefOf.VV_Effecter_ApocryphaNeedleAim, _pos);
        }

        public bool CancelAim()
        {
            if (_state != NeedleState.Aiming || IsQueen) { return false; }

            _state = NeedleState.Idle;
            _stateStartTick = GenTicks.TicksGame;
            return true;
        }

        public bool PullOut(IntVec3 cell)
        {
            if (_state != NeedleState.Stuck || !Spawned) { return false; }

            PlayEffectAt(VVEffecterDefOf.VV_Effecter_ApocryphaNeedlePullOut, _pos);
            ScatterTo(cell, -_laneDir);
            return true;
        }

        public void ScatterTo(IntVec3 cell, Vector3 outDir)
        {
            if (!Spawned || !cell.IsValid) { return; }

            var now = GenTicks.TicksGame;
            var tip = cell.ToVector3Shifted().Yto0();
            var travel = (tip - _pos).Yto0();
            var normalizedOut = EndingVfxUtility.SafeNormalizeHorizontal(outDir, Vector3.forward);
            var direction = EndingVfxUtility.SafeNormalizeHorizontal(travel, normalizedOut);

            _groundCell = cell;
            _plantTip = tip;
            _scatterFrom = _pos;
            _scatterOut = EndingVfxUtility.SafeNormalizeHorizontal(outDir, direction);
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

            _lockTarget = activator;
            _laneStart = ApocryphaNeedleLane.ClampInsideMap(Map, _plantTip);
            _laneDir = EndingVfxUtility.SafeNormalizeHorizontal(_plantTip - _pos, Vector3.forward);
            UpdateLockDirection();

            _moveFrom = _pos;
            _moveFromAngle = _angle;
            _activateTick = now;
            _lockEndTick = now + Mathf.Max(1, BossComp.Props.needleLockOnTicks);
            _state = NeedleState.LockOn;
            _stateStartTick = now;

            PlayEffectAt(VVEffecterDefOf.VV_Effecter_ApocryphaNeedleAim, _plantTip);
            Messages.Message(LocalizeString_Message.VV_Message_ApocryphaNeedleActivated.Translate(activator.Named("PAWN")), this, MessageTypeDefOf.CautionInput, historical: false);
        }

        public static ApocryphaNeedle FindPlantedNear(Map map, Vector3 clickPos)
        {
            var def = VVThingDefOf.VV_ApocryphaNeedle;
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

        public void AttachStuck(Vector3 direction)
        {
            if (!Spawned || _boss == null || !_boss.Spawned) { return; }

            _laneDir = EndingVfxUtility.SafeNormalizeHorizontal(direction, Vector3.forward);
            SetStuck(GenTicks.TicksGame - StuckShakeTicks);
        }

        public void Recall()
        {
            if (!Spawned || (_state != NeedleState.Planted && _state != NeedleState.Scattering && _state != NeedleState.Stuck)) { return; }

            _lockTarget = null;
            StartReturn(GenTicks.TicksGame, _pos, EndingVfxUtility.SafeNormalizeHorizontal(_pos - BodyCenter, Vector3.forward));
        }

        public void DebugStick()
        {
            if (!Spawned || !IsQueen || _state == NeedleState.Stuck || IsFlying) { return; }

            _laneDir = EndingVfxUtility.SafeNormalizeHorizontal(BodyCenter - _pos, Vector3.forward);
            Stick(GenTicks.TicksGame);
        }

        public override void Kill(DamageInfo? dinfo = null, Hediff exactCulprit = null)
        {
            if (dinfo?.Category == DamageInfo.SourceCategory.Collapse) { return; }

            base.Kill(dinfo, exactCulprit);
        }

        public void Vanish()
        {
            if (Destroyed) { return; }

            if (Spawned)
            {
                PlayEffectAt(VVEffecterDefOf.VV_Effecter_ApocryphaNeedleVanish, _pos);
            }

            Destroy(DestroyMode.Vanish);
        }
        #endregion

        #region 틱
        protected override void Tick()
        {
            base.Tick();
            if (!Spawned) { return; }

            var comp = BossComp;
            if (comp == null || comp.Phase == ApocryphaPhase.Dying || _boss.MapHeld != Map
                || (comp.Phase == ApocryphaPhase.Rebirth ? !IsPlanted && !IsScattering : !_boss.Spawned))
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
            var detach = ApocryphaUtility.EaseInOut(Mathf.Clamp01(elapsed / (float)DetachTicks));
            var windup = WindupDistance * Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.55f, 1f, AimProgress));

            var hold = _laneStart - _laneDir * windup;
            if (_fireTick - now <= ShakeTicks)
            {
                hold += ApocryphaNeedleLane.JitterPerpendicular(_laneDir, ShakeAmplitude);
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

            _stickAlong = IsQueen ? BossContactAlong() : -1f;

            _pos = BodyOnLane(_travelled);
            Trail?.ResetTrail();
            RegisterTrail(_pos);

            PlayEffectAt(VVEffecterDefOf.VV_Effecter_ApocryphaNeedleFire, _pos);
            ApocryphaUtility.ShakeCamera(Map, 0.5f);

            if (_stickAlong < 0f)
            {
                DamageAlong(0f, Mathf.Clamp(_travelled, 0f, _laneLength));
                if (Destroyed) { return; }
            }

            TickFiring(now);
        }

        private void TickFiring(int now)
        {
            var from = _travelled;

            if (_stickAlong < 0f && IsQueen)
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
                Trail?.FinishTrail();
                PlayEffectAt(VVEffecterDefOf.VV_Effecter_ApocryphaNeedleEnd, PointOnLane(_laneLength));
            }
        }

        private void TickLodged(int now)
        {
            var elapsed = now - _stateStartTick;
            var shake = ShakeAmplitude * (1f - elapsed / (float)LodgedTicks);
            _pos = BodyOnLane(_laneLength) + ApocryphaNeedleLane.JitterPerpendicular(_laneDir, shake);

            if (elapsed >= LodgedTicks)
            {
                if (IsQueen)
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
            if (cell.Standable(map) && !cell.Fogged(map))
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
                if (back.InBounds(map) && back.Standable(map) && !back.Fogged(map))
                {
                    ScatterTo(back, -_laneDir);
                    return;
                }
            }

            var near = CellFinder.StandableCellNear(cell, map, 6f, c => c.InBounds(map) && !c.Fogged(map));
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
            PlayEffectAt(VVEffecterDefOf.VV_Effecter_ApocryphaNeedlePlant, _plantTip);
            ApocryphaUtility.ShakeCamera(Map, 0.3f);
        }

        private void TickScattering(int now)
        {
            var t = Mathf.Clamp01((now - _stateStartTick) / (float)Mathf.Max(1, _scatterTicks));
            var inDir = EndingVfxUtility.SafeNormalizeHorizontal(_plantTip - _scatterTo, Vector3.forward, minSqrMagnitude: 0.0001f);
            var c1 = _scatterFrom + _scatterOut * ScatterOutWeight + new Vector3(0f, 0f, ScatterLift);
            var c2 = _scatterTo - inDir * ScatterInWeight + new Vector3(0f, 0f, ScatterLift * 0.5f);

            UpdateBezierFlight(_scatterFrom, c1, c2, _scatterTo, ApocryphaUtility.EaseInOut(t), t, 0.7f, _scatterAngle);

            if (t >= 1f)
            {
                Plant(now);
            }
        }

        private void TickPlanted(int now)
        {
            if ((now + thingIDNumber) % PlantedGlintInterval != 0) { return; }

            var map = Map;
            if (!_plantTip.ShouldSpawnMotesAt(map)) { return; }

            var offset = new Vector3(Rand.Range(-0.4f, 0.4f), 0f, Rand.Range(-0.2f, 0.5f));
            var data = FleckMaker.GetDataStatic(_plantTip + offset, map, VVFleckDefOf.VV_Fleck_ApocryphaAimTwinkle, Rand.Range(0.3f, 0.5f));
            data.velocityAngle = Rand.Range(-30f, 30f);
            data.velocitySpeed = Rand.Range(0.2f, 0.6f);
            data.rotation = Rand.Range(0f, 360f);
            map.flecks.CreateFleck(data);
        }

        private void TickLockOn(int now)
        {
            UpdateLockDirection();

            var elapsed = now - _stateStartTick;
            var lift = ApocryphaUtility.EaseInOut(Mathf.Clamp01(elapsed / (float)LiftTicks));
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
            _aimTicks = Mathf.Max(1, BossComp?.Props.needleLockedTicks ?? 112);
            _fireTick = now + _aimTicks;
            _state = NeedleState.Aiming;
            _stateStartTick = now;

            PlayEffectAt(VVEffecterDefOf.VV_Effecter_ApocryphaNeedleAim, _pos);
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

            _laneLength = ApocryphaNeedleLane.LengthInsideMap(Map, _laneStart, _laneDir, BossComp?.Props.needleRange ?? 19f);
        }

        private void Stick(int now)
        {
            SetStuck(now);

            PlayEffectAt(VVEffecterDefOf.VV_Effecter_ApocryphaNeedleStick, BodyCenter);
            ApocryphaUtility.ShakeCamera(Map, 0.7f);

            BossComp?.NeedleAttack.Notify_NeedleStuck();
        }

        private void SetStuck(int now)
        {
            _state = NeedleState.Stuck;
            _stateStartTick = now;
            _stickAlong = -1f;
            _stuckAngle = LaneAngle;
            _lockTarget = null;

            var perpendicular = EndingVfxUtility.HorizontalPerpendicular(_laneDir);
            _stuckOffset = -_laneDir * StuckDepth + perpendicular * ((_slot - 1) * StuckSpread);
            _pos = BodyCenter + _stuckOffset;
            _angle = _stuckAngle;

            Trail?.FinishTrail();
        }

        private void TickStuck(int now)
        {
            var elapsed = now - _stateStartTick;
            _pos = BodyCenter + _stuckOffset;
            _angle = _stuckAngle;

            if (elapsed < StuckShakeTicks)
            {
                var shake = ShakeAmplitude * (1f - elapsed / (float)StuckShakeTicks);
                _pos += ApocryphaNeedleLane.JitterPerpendicular(_laneDir, shake);
            }
        }

        private void StartReturn(int now, Vector3 from, Vector3 outDir)
        {
            _state = NeedleState.Returning;
            _stateStartTick = now;
            _returnFrom = from;
            _returnOutDir = outDir;
            _returnSide = ResolveReturnSide(from, outDir, SlotPos(now));
            _returnProgress = 0f;

            GetReturnCurve(now, out var p0, out var c1, out var c2, out var p3);
            var chord = Vector3.Distance(p0, p3);
            var polygon = Vector3.Distance(p0, c1) + Vector3.Distance(c1, c2) + Vector3.Distance(c2, p3);
            _returnLength = Mathf.Max(1f, (chord + polygon) * 0.5f);

            Trail?.FinishTrail();
            RegisterTrail(from);
        }

        private void TickReturning(int now)
        {
            _returnProgress = Mathf.Min(1f, _returnProgress + ReturnSpeed / Mathf.Max(1f, _returnLength));

            GetReturnCurve(now, out var p0, out var c1, out var c2, out var p3);
            UpdateBezierFlight(p0, c1, c2, p3, _returnProgress, _returnProgress, ReturnBlendStart, SlotAngle(now), startAngle: LaneAngle, startBlendEnd: ReturnStartBlendEnd);

            if (_returnProgress >= 1f)
            {
                _state = NeedleState.Idle;
                _stateStartTick = now;
                Trail?.FinishTrail();
            }
        }

        private void UpdateBezierFlight(Vector3 p0, Vector3 c1, Vector3 c2, Vector3 p3, float curveT, float blendT, float blendStart, float targetAngle, float? startAngle = null, float startBlendEnd = 0f)
        {
            _pos = Bezier(p0, c1, c2, p3, curveT);

            var travel = BezierDerivative(p0, c1, c2, p3, curveT).Yto0();
            var travelAngle = travel.sqrMagnitude > 0.0001f ? travel.AngleFlat() : _angle;
            if (startAngle.HasValue && startBlendEnd > 0f && blendT < startBlendEnd)
            {
                var startBlend = Mathf.SmoothStep(0f, 1f, blendT / startBlendEnd);
                travelAngle = LerpAngle(startAngle.Value, travelAngle, startBlend);
            }

            var angleBlend = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(blendStart, 1f, blendT));
            _angle = LerpAngle(travelAngle, targetAngle, angleBlend);

            RegisterTrail(_pos);
        }

        private float ResolveReturnSide(Vector3 from, Vector3 outDir, Vector3 slotPos)
        {
            var perp = EndingVfxUtility.HorizontalPerpendicular(outDir);
            var startCross = Vector3.Dot((_laneStart - BodyCenter).Yto0(), perp);
            if (Mathf.Abs(startCross) > 0.5f)
            {
                return -Mathf.Sign(startCross);
            }

            var slotCross = Vector3.Dot((slotPos - from).Yto0(), perp);
            if (Mathf.Abs(slotCross) > 0.5f)
            {
                return Mathf.Sign(slotCross);
            }

            return _slot % 2 == 0 ? 1f : -1f;
        }

        private void GetReturnCurve(int now, out Vector3 p0, out Vector3 c1, out Vector3 c2, out Vector3 p3)
        {
            p0 = _returnFrom;
            p3 = SlotPos(now);

            var chord = (p3 - p0).MagnitudeHorizontal();
            var perp = EndingVfxUtility.HorizontalPerpendicular(_returnOutDir);
            var side = _returnSide < 0f ? -1f : 1f;
            var outWeight = Mathf.Min(ReturnOutWeight, Mathf.Max(3.5f, chord * 0.3f));
            var arc = Mathf.Clamp(chord * ReturnArcFactor, ReturnArcMin, ReturnArcMax);
            var outward = EndingVfxUtility.SafeNormalizeHorizontal(p3 - BodyCenter, Vector3.forward, minSqrMagnitude: 0.0001f);

            c1 = p0 + _returnOutDir * outWeight + perp * (side * arc * ReturnStartSideRatio);
            c2 = Vector3.Lerp(p0, p3, 0.6f) + perp * (side * arc) + outward * ReturnInWeight;
        }
        #endregion

        #region 피해
        private void DamageAlong(float from, float to)
        {
            var map = Map;
            var comp = BossComp;
            if (map == null || comp == null || to <= from) { return; }

            EnsureIgnoredThings(map);
            ApocryphaNeedleLane.PierceAlong(this, _laneStart, _laneDir, _laneLength, comp.Props.needleHalfWidth, from, to, _hitIds, CanPierce, thing => Pierce(thing, comp));
        }

        private void EnsureIgnoredThings(Map map)
        {
            _ignored ??= ApocryphaGimmicks.GetBlastIgnoredThings(map, _boss);
        }

        private bool CanPierce(Thing thing)
        {
            if (_boss != null && thing.Faction != null && thing.Faction == _boss.Faction) { return false; }

            return !_ignored.Contains(thing);
        }

        private void Pierce(Thing thing, CompApocrypha comp)
        {
            var damage = thing is Pawn pawn && pawn.IsVivi() ? comp.Props.needleDamage * comp.Props.needleDamageFactorVivi : comp.Props.needleDamage;
            ApocryphaNeedleLane.Pierce(thing, damage, comp.Props.needleArmorPenetration, LaneAngle, _boss);
        }
        #endregion

        #region 그리기
        protected override void DrawAt(Vector3 drawLoc, bool flip = false)
        {
            var now = GenTicks.TicksGame;
            var alpha = Mathf.Clamp01((now - _spawnTick) / (float)FadeInTicks);
            if (alpha <= 0f) { return; }

            if (_state == NeedleState.Aiming || _state == NeedleState.LockOn)
            {
                DrawAimLane(now, alpha);
            }

            var warpAge = now - _fireTick;
            if (_fireTick >= 0 && (IsFlying || IsStuck || IsPlanted) && warpAge >= 0 && warpAge < WarpTicks)
            {
                ApocryphaNeedleRenderer.DrawWarp(_laneStart, _laneDir, _laneLength, Mathf.Max(0f, _travelled), warpAge / 60f, alpha);
            }

            if (IsStuck || IsPlanted)
            {
                var stuckSecs = (now - _stateStartTick) / 60f;
                if (IsPlanted)
                {
                    ApocryphaNeedleRenderer.DrawPlantedMarker(_plantTip, stuckSecs, alpha, thingIDNumber);
                }

                ApocryphaNeedleRenderer.DrawStuckNeedle(DrawPos, _angle, alpha, stuckSecs);
                return;
            }

            ApocryphaNeedleRenderer.DrawNeedle(DrawPos, _angle, alpha, ComputeGlow(now));
        }

        private void DrawAimLane(int now, float alpha)
        {
            var queen = IsQueen;
            var bossInLane = queen && BossContactAlong() >= 0f;
            float progress;
            float aimSecs;
            float aimDuration;

            if (queen)
            {
                var lockTicks = Mathf.Max(1, _lockEndTick - _activateTick);
                progress = _state == NeedleState.LockOn
                    ? 0.5f * Mathf.Clamp01((now - _activateTick) / (float)lockTicks)
                    : 0.5f + 0.5f * AimProgress;
                aimSecs = (now - _activateTick) / 60f;
                aimDuration = (lockTicks + (BossComp?.Props.needleLockedTicks ?? 112)) / 60f;
            }
            else
            {
                progress = AimProgress;
                aimSecs = (now - _stateStartTick) / 60f;
                aimDuration = _aimTicks / 60f;
            }

            ApocryphaNeedleRenderer.DrawLane(
                start: _laneStart, direction: _laneDir, length: _laneLength, width: HalfWidth * 2f,
                progress: progress, alpha: alpha, bossInside: bossInLane,
                aimSecs: aimSecs, aimDuration: aimDuration, seed: thingIDNumber);
        }

        private float ComputeGlow(int now)
        {
            switch (_state)
            {
                case NeedleState.Idle:
                    return 0f;

                case NeedleState.Aiming:
                    return AimProgress;

                case NeedleState.LockOn:
                    return 0.5f * Mathf.Clamp01((now - _activateTick) / (float)Mathf.Max(1, _lockEndTick - _activateTick));

                default:
                    return 1f;
            }
        }
        #endregion

        #region 도우미
        private float? ResolveCloseTargetFanAngle(Vector3 origin, Vector3 target, Vector3 direction)
        {
            if (Vector3.Distance(origin, target) >= LaunchOffset + CloseTargetMargin) { return null; }

            var radius = BossComp?.Props.needleFanRadius ?? 5f;
            var left = origin + Quaternion.AngleAxis(-CloseTargetFanAngle, Vector3.up) * direction * radius;
            var right = origin + Quaternion.AngleAxis(CloseTargetFanAngle, Vector3.up) * direction * radius;
            var currentPos = _pos.Yto0();
            return (left - currentPos).sqrMagnitude <= (right - currentPos).sqrMagnitude ? -CloseTargetFanAngle : CloseTargetFanAngle;
        }

        private float BossContactAlong()
        {
            if (_boss == null || !_boss.Spawned || _laneLength <= 0f) { return -1f; }

            var best = -1f;
            var reach = HalfWidth + BossHitRadius;
            for (int i = 0; i < 2; i++)
            {
                var point = i == 0 ? _boss.DrawPos.Yto0() : BodyCenter;
                ApocryphaNeedleLane.Project(_laneStart, _laneDir, point, out var along, out var across);
                if (along < -BossHitRadius || along > _laneLength + BossHitRadius || across > reach) { continue; }

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
            if (IsQueen)
            {
                var bob = SlotBobHeight * Mathf.Sin(now * 0.05f + _slot * 1.9f);
                var orbit = OrbitDegrees(now) * Mathf.Deg2Rad;
                return BodyCenter + new Vector3(Mathf.Cos(orbit) * OrbitRadiusX, 0f, Mathf.Sin(orbit) * OrbitRadiusZ + bob);
            }

            return BodyCenter + new Vector3(0f, 0f, HaloLift + HaloBobHeight * Mathf.Sin(now * 0.04f)) + HaloDirection(now) * (HaloRootRadius + HaloCenterFromRoot);
        }

        private Vector3 HaloDirection(int now)
        {
            var count = Mathf.Max(1, BossComp?.NeedleAttack.NeedleCount ?? 1);
            var half = (count + 1) / 2;
            var order = _slot < half ? _slot * 2 : (_slot - half) * 2 + 1;
            var t = count > 1 ? order / (float)(count - 1) : 0.5f;
            var angle = (Mathf.Lerp(-HaloSpreadDegrees, HaloSpreadDegrees, t) + HaloSwayDegrees * Mathf.Sin(now * 0.02f + _slot * 0.9f)) * Mathf.Deg2Rad;
            return new Vector3(Mathf.Sin(angle), 0f, Mathf.Cos(angle));
        }

        private float OrbitDegrees(int now)
        {
            var count = Mathf.Max(1, BossComp?.NeedleAttack.NeedleCount ?? 1);
            return _slot * 360f / count + (now % 1440) * OrbitDegreesPerTick;
        }

        private float SlotAngle(int now)
        {
            if (!IsQueen)
            {
                return HaloDirection(now).AngleFlat();
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
