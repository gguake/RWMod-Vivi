using RimWorld;
using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace VVRace
{
    [StaticConstructorOnStartup]
    public class ApocryphaFairyBond : IExposable
    {
        public struct ReturnFairyLight
        {
            public Vector3 startPos;
            public Vector3 fixedTargetPos;
            public Vector3 pos;
            public Vector3 prevPos;
            public Vector3 targetOffset;
            public int startTick;
            public int durationTicks;
            public float arcSign;
            public float arcAmplitude;
            public float swirlFreq;
            public float swirlPhase;
            public float scale;
            public float seed;
            public bool fromEverflower;
            public bool toEverflower;
        }

        private const int DrainStreamTicks = 30;
        private const int DecayStreamInterval = 7;
        private const int MaxReturnLights = 128;
        private const float MaxPendingLights = 48f;
        private const float DrainLightsPerMaxBond = 220f;
        private const float RecoverLightsPerMaxBond = 220f;
        private const int MinGuaranteedDrainBurstIntervalTicks = 15;
        private const int DepartureBurstCooldownTicks = 24;
        private const int ArrivalBurstCooldownTicks = 10;
        private const int TrailSparkIntervalTicks = 6;
        private const float ReturnCellsPerTick = 0.28f;
        private const int MinReturnDurationTicks = 52;
        private const int MaxReturnDurationTicks = 155;
        private const float SourceJitter = 0.8f;
        private const float TargetJitter = 1.4f;

        private static readonly FloatRange DecayScale = new FloatRange(0.58f, 0.88f);
        private static readonly FloatRange DrainScale = new FloatRange(0.76f, 1.18f);
        private static readonly FloatRange RecoverScale = new FloatRange(0.78f, 1.22f);
        private static readonly FloatRange ViviReturnScale = new FloatRange(0.88f, 1.32f);
        private static readonly Color DrainLightColor = new Color(1f, 0.93f, 0.66f, 1f);
        private static readonly Color RecoverLightColor = new Color(1f, 0.91f, 0.60f, 1f);
        private static readonly Color ViviReturnLightColor = new Color(1f, 0.96f, 0.82f, 1f);

        private static readonly Material HaloMat = MaterialPool.MatFrom("VFX/VV_ScentAura", ShaderDatabase.MoteGlow, Color.white);
        private static readonly Material CometMat = MaterialPool.MatFrom("VFX/VV_ScentComet", ShaderDatabase.MoteGlow, Color.white);
        private static readonly Material CoreMat = MaterialPool.MatFrom("Things/Mote/SparkFlash", ShaderDatabase.MoteGlow, Color.white);
        private static readonly Material TwinkleMat = MaterialPool.MatFrom("VFX/VV_ScentTwinkle", ShaderDatabase.MoteGlow, Color.white);
        private static readonly MaterialPropertyBlock PropertyBlock = new MaterialPropertyBlock();

        private readonly CompApocrypha _comp;

        private float _bond = -1f;
        private int _lastDrainTick = -99999;
        private int _recoverFlowUntilTick = -1;
        private float _pendingDrainLights;
        private float _pendingRecoverLights;

        [Unsaved]
        private readonly List<ReturnFairyLight> _returnLights = new List<ReturnFairyLight>();
        [Unsaved]
        private int _nextArcSign = 1;
        [Unsaved]
        private int _lastDrainDepartureBurstTick = -99999;
        [Unsaved]
        private int _lastDrainGuaranteedTick = -99999;
        [Unsaved]
        private int _lastRecoverDepartureBurstTick = -99999;
        [Unsaved]
        private int _lastBossArrivalBurstTick = -99999;
        [Unsaved]
        private int _lastBudArrivalBurstTick = -99999;
        [Unsaved]
        private int _totalDrainLightsSpawned;
        [Unsaved]
        private int _totalRecoverLightsSpawned;

        private CompProperties_Apocrypha Props => _comp.Props;

        public float Max => Props.fairyBondInitial * _comp.FairyBondDifficultyFactor;
        public float Value => Mathf.Max(0f, _bond);
        public float Fraction => Max > 0f ? Mathf.Clamp01(Value / Max) : 0f;
        public bool Draining => GenTicks.TicksGame - _lastDrainTick <= DrainStreamTicks || _pendingDrainLights >= 1f;
        public bool IsDecaying => DecayPerHour > 0f;
        public int ActiveReturnLightCount => _returnLights.Count;
        public float PendingDrainLights => _pendingDrainLights;
        public float PendingRecoverLights => _pendingRecoverLights;
        public int TotalDrainLightsSpawned => _totalDrainLightsSpawned;
        public int TotalRecoverLightsSpawned => _totalRecoverLightsSpawned;
        public IReadOnlyList<ReturnFairyLight> ReturnLights => _returnLights;

        public float DecayPerHour
        {
            get
            {
                var fraction = Fraction;
                if (fraction >= Props.fairyBondDecayStart) { return 0f; }

                var t = Mathf.Clamp01((Props.fairyBondDecayStart - fraction) / Props.fairyBondDecayRampWidth);
                return Props.fairyBondInitial * Props.fairyBondDecayPerHour.LerpThroughRange(t);
            }
        }

        public ApocryphaFairyBond(CompApocrypha comp)
        {
            _comp = comp;
        }

        public void ExposeData()
        {
            Scribe_Values.Look(ref _bond, "bond", -1f);
            Scribe_Values.Look(ref _lastDrainTick, "lastDrainTick", -99999);
            Scribe_Values.Look(ref _recoverFlowUntilTick, "recoverFlowUntilTick", -1);
            Scribe_Values.Look(ref _pendingDrainLights, "pendingDrainLights", 0f);
            Scribe_Values.Look(ref _pendingRecoverLights, "pendingRecoverLights", 0f);
        }

        public void Reset()
        {
            _bond = Max;
            _lastDrainTick = -99999;
            _recoverFlowUntilTick = -1;
            _pendingDrainLights = 0f;
            _pendingRecoverLights = 0f;
            _returnLights.Clear();
        }

        public void ClearVisuals()
        {
            _pendingDrainLights = 0f;
            _pendingRecoverLights = 0f;
            _returnLights.Clear();
        }

        public void Tick(int now)
        {
            var decayPerHour = DecayPerHour;
            if (decayPerHour > 0f)
            {
                _bond = Mathf.Max(0f, _bond - decayPerHour / GenDate.TicksPerHour);
                CheckExhausted();
            }

            TickStream(now, decayPerHour > 0f);
            TickReturnLights(now);
        }

        public void Drain(float amount)
        {
            if (amount <= 0f) { return; }

            var now = GenTicks.TicksGame;
            var wasDraining = Draining;
            var wasDecaying = IsDecaying;
            var prevBond = Value;
            _bond = Mathf.Max(0f, _bond - amount);
            var actualLost = Mathf.Max(0f, prevBond - Value);
            var lostFraction = Max > 0f ? actualLost / Max : 0f;
            _lastDrainTick = now;
            BeginDrainFlow(now, wasDraining, lostFraction);
            CheckExhausted();

            if (!wasDecaying && IsDecaying)
            {
                _comp.Notify_BondDecayStarted();
            }
        }

        public void DrainMaxFraction(float fraction)
        {
            if (fraction <= 0f) { return; }

            Drain(Max * fraction);
        }

        public void Recover(float fraction)
        {
            if (fraction <= 0f || _bond >= Max) { return; }

            var wasDecaying = IsDecaying;
            var prevBond = Value;
            _bond = Mathf.Min(Max, _bond + Props.fairyBondInitial * fraction);
            var actualGained = Mathf.Max(0f, Value - prevBond);
            var gainedFraction = Max > 0f ? actualGained / Max : 0f;
            BeginRecoverFlow(GenTicks.TicksGame, gainedFraction);

            if (wasDecaying && !IsDecaying)
            {
                _comp.Notify_Approached();
            }
        }

        public void RecoverMaxFraction(float fraction, bool showStream = true)
        {
            if (fraction <= 0f || _bond >= Max) { return; }

            var wasDecaying = IsDecaying;
            var prevBond = Value;
            _bond = Mathf.Min(Max, _bond + Max * fraction);
            var actualGained = Mathf.Max(0f, Value - prevBond);
            var gainedFraction = Max > 0f ? actualGained / Max : 0f;
            if (showStream)
            {
                BeginRecoverFlow(GenTicks.TicksGame, gainedFraction);
            }

            if (wasDecaying && !IsDecaying)
            {
                _comp.Notify_Approached();
            }
        }

        public void SpawnViviReturnBurst(Vector3 origin, int count = 6)
        {
            var map = _comp.Pawn.Map;
            if (map == null || !_comp.Pawn.Spawned || count <= 0) { return; }

            var now = GenTicks.TicksGame;
            var baseOrigin = origin.Yto0();
            var target = _comp.BodyCenter.Yto0();
            for (int i = 0; i < count; i++)
            {
                var start = baseOrigin + Gen.RandomHorizontalVector(0.45f);
                SpawnReturnLight(now, start, target, fromEverflower: false, toEverflower: false, ViviReturnScale.RandomInRange);
            }
        }

        public void Draw()
        {
            if (_returnLights.Count == 0 || !_comp.Pawn.Spawned) { return; }

            var map = _comp.Pawn.Map;
            var now = GenTicks.TicksGame;
            var age = LinkRenderUtility.WrappedAgeSecs;
            var baseY = AltitudeLayer.MoteOverhead.AltitudeFor() + 0.02f;
            var bossTarget = _comp.BodyCenter.Yto0();
            var budTarget = Vector3.zero;
            var hasBud = map != null && ApocryphaGimmicks.TryGetEverflowerBudPos(map, out budTarget);

            for (int i = 0; i < _returnLights.Count; i++)
            {
                var light = _returnLights[i];
                var target = ResolveTarget(light, bossTarget, hasBud, budTarget);
                var u = Mathf.Clamp01((now - light.startTick) / (float)Mathf.Max(1, light.durationTicks));
                var pos = EvaluatePosition(light, target, u);
                var ahead = EvaluatePosition(light, target, Mathf.Min(1f, u + 0.03f));
                var heading = (ahead - pos).Yto0();
                if (heading.sqrMagnitude < 0.0001f)
                {
                    heading = (pos - EvaluatePosition(light, target, Mathf.Max(0f, u - 0.03f))).Yto0();
                }

                var fadeIn = Mathf.Clamp01(u / 0.14f);
                var fadeOut = 1f - Mathf.SmoothStep(0.84f, 1f, u);
                var alpha = fadeIn * fadeOut;
                if (alpha <= 0.01f) { continue; }

                var pulse = 0.88f + 0.14f * Mathf.Sin(age * 11f + light.seed * 2.3f);
                var baseColor = GetLightColor(light);
                var scale = light.scale * (0.85f + 0.25f * Mathf.Sin(u * Mathf.PI)) * pulse;
                var drawPos = new Vector3(pos.x, baseY, pos.z);

                var haloScale = 1.95f * scale;
                PropertyBlock.Clear();
                PropertyBlock.SetColor(ShaderPropertyIDs.Color, new Color(baseColor.r, baseColor.g, baseColor.b, 0.72f * alpha));
                Graphics.DrawMesh(MeshPool.plane10, Matrix4x4.TRS(drawPos, Quaternion.identity, new Vector3(haloScale, 1f, haloScale)), HaloMat, 0, null, 0, PropertyBlock);

                var hasHeading = heading.sqrMagnitude > 0.0001f;
                var headingDir = hasHeading ? heading.normalized : Vector3.forward;
                var cometAngle = hasHeading ? heading.AngleFlat() + 180f : 0f;
                var cometRot = Quaternion.AngleAxis(cometAngle, Vector3.up);
                var cometPos = drawPos - headingDir * (0.38f * scale) + new Vector3(0f, 0.008f, 0f);
                PropertyBlock.Clear();
                PropertyBlock.SetColor(ShaderPropertyIDs.Color, new Color(baseColor.r, baseColor.g, baseColor.b, 0.96f * alpha));
                Graphics.DrawMesh(MeshPool.plane10, Matrix4x4.TRS(cometPos, cometRot, new Vector3(1.1f * scale, 1f, 1.8f * scale)), CometMat, 0, null, 0, PropertyBlock);

                var coreColor = Color.Lerp(baseColor, Color.white, 0.6f);
                coreColor.a = 0.95f * alpha;
                var coreScale = 0.72f * scale;
                PropertyBlock.Clear();
                PropertyBlock.SetColor(ShaderPropertyIDs.Color, coreColor);
                Graphics.DrawMesh(MeshPool.plane10, Matrix4x4.TRS(drawPos + new Vector3(0f, 0.016f, 0f), Quaternion.identity, new Vector3(coreScale, 1f, coreScale)), CoreMat, 0, null, 0, PropertyBlock);

                var twinkleScale = 0.92f * scale * (1.08f - 0.14f * pulse);
                var twinkleRot = Quaternion.AngleAxis((age * 75f + light.seed * 60f) % 360f, Vector3.up);
                PropertyBlock.Clear();
                PropertyBlock.SetColor(ShaderPropertyIDs.Color, new Color(1f, 0.99f, 0.92f, 0.9f * alpha));
                Graphics.DrawMesh(MeshPool.plane10, Matrix4x4.TRS(drawPos + new Vector3(0f, 0.024f, 0f), twinkleRot, new Vector3(twinkleScale, 1f, twinkleScale)), TwinkleMat, 0, null, 0, PropertyBlock);
            }
        }

        private void BeginDrainFlow(int now, bool wasDraining, float lostFraction)
        {
            var added = lostFraction * DrainLightsPerMaxBond;
            if (added <= 0f) { return; }

            _pendingDrainLights = Mathf.Min(MaxPendingLights, _pendingDrainLights + added);
            if (_pendingDrainLights < 1f && (!wasDraining || now - _lastDrainGuaranteedTick >= MinGuaranteedDrainBurstIntervalTicks))
            {
                _pendingDrainLights = 1f;
                _lastDrainGuaranteedTick = now;
            }

            var map = _comp.Pawn.Map;
            if (map == null || !_comp.Pawn.Spawned || !ApocryphaGimmicks.TryGetEverflowerBudPos(map, out var bud)) { return; }

            var body = _comp.BodyCenter.Yto0();
            if ((!wasDraining || added >= 8f) && now - _lastDrainDepartureBurstTick >= DepartureBurstCooldownTicks)
            {
                _lastDrainDepartureBurstTick = now;
                ThrowDepartureBurst(map, body, DrainLightColor);
            }

            var immediateCount = Mathf.Clamp(Mathf.FloorToInt(added * 0.25f), !wasDraining && _pendingDrainLights >= 1f ? 1 : 0, 6);
            immediateCount = Mathf.Min(immediateCount, Mathf.FloorToInt(_pendingDrainLights));
            for (int i = 0; i < immediateCount; i++)
            {
                _pendingDrainLights -= 1f;
                SpawnReturnLight(now, body + Gen.RandomHorizontalVector(SourceJitter * 0.75f), bud, fromEverflower: false, toEverflower: true, DrainScale.RandomInRange);
                _totalDrainLightsSpawned++;
            }
        }

        private void BeginRecoverFlow(int now, float gainedFraction)
        {
            var added = gainedFraction * RecoverLightsPerMaxBond;
            if (added <= 0f) { return; }

            var wasActive = _pendingRecoverLights >= 1f;
            var scaledAdd = Mathf.Max(2f, added);
            _pendingRecoverLights = Mathf.Min(MaxPendingLights, _pendingRecoverLights + scaledAdd);

            var map = _comp.Pawn.Map;
            if (map == null || !_comp.Pawn.Spawned || !ApocryphaGimmicks.TryGetEverflowerBudPos(map, out var bud)) { return; }

            var target = _comp.BodyCenter.Yto0();
            if ((!wasActive || added >= 8f) && now - _lastRecoverDepartureBurstTick >= DepartureBurstCooldownTicks)
            {
                _lastRecoverDepartureBurstTick = now;
                ThrowDepartureBurst(map, bud, RecoverLightColor);
            }

            var immediateCount = Mathf.Clamp(Mathf.FloorToInt(scaledAdd * 0.25f), 1, 6);
            immediateCount = Mathf.Min(immediateCount, Mathf.FloorToInt(_pendingRecoverLights));
            for (int i = 0; i < immediateCount; i++)
            {
                _pendingRecoverLights -= 1f;
                SpawnReturnLight(now, bud + Gen.RandomHorizontalVector(TargetJitter * 0.65f), target, fromEverflower: true, toEverflower: false, RecoverScale.RandomInRange);
                _totalRecoverLightsSpawned++;
            }
        }

        private void CheckExhausted()
        {
            if (Fraction <= Props.fairyBondClear)
            {
                _comp.Notify_BondExhausted();
            }
        }

        private void TickStream(int now, bool decaying)
        {
            var map = _comp.Pawn.Map;
            if (!ApocryphaGimmicks.TryGetEverflowerBudPos(map, out var bud)) { return; }

            var body = _comp.BodyCenter.Yto0();
            if (_pendingDrainLights >= 1f)
            {
                var interval = StreamIntervalForPending(_pendingDrainLights);
                if (now % interval == 0)
                {
                    var burst = _pendingDrainLights >= 24f && Rand.Chance(0.45f) ? 2 : 1;
                    for (int i = 0; i < burst && _pendingDrainLights >= 1f; i++)
                    {
                        _pendingDrainLights -= 1f;
                        var scale = i == 0 ? DrainScale.RandomInRange : DrainScale.RandomInRange * 0.85f;
                        SpawnReturnLight(now, body + Gen.RandomHorizontalVector(SourceJitter * (i == 0 ? 1f : 1.1f)), bud, fromEverflower: false, toEverflower: true, scale);
                        _totalDrainLightsSpawned++;
                    }
                }
            }
            else if (decaying && now % DecayStreamInterval == 0)
            {
                SpawnReturnLight(now, body + Gen.RandomHorizontalVector(SourceJitter), bud, fromEverflower: false, toEverflower: true, DecayScale.RandomInRange);
                _totalDrainLightsSpawned++;
            }

            if (_pendingRecoverLights >= 1f)
            {
                var interval = StreamIntervalForPending(_pendingRecoverLights);
                if (now % interval == 0)
                {
                    var burst = _pendingRecoverLights >= 24f && Rand.Chance(0.45f) ? 2 : 1;
                    for (int i = 0; i < burst && _pendingRecoverLights >= 1f; i++)
                    {
                        _pendingRecoverLights -= 1f;
                        var scale = i == 0 ? RecoverScale.RandomInRange : RecoverScale.RandomInRange * 0.85f;
                        SpawnReturnLight(now, bud + Gen.RandomHorizontalVector(TargetJitter * (i == 0 ? 0.75f : 0.85f)), body, fromEverflower: true, toEverflower: false, scale);
                        _totalRecoverLightsSpawned++;
                    }
                }
            }
        }

        private static int StreamIntervalForPending(float pending)
        {
            if (pending >= 18f) { return 2; }
            if (pending >= 6f) { return 3; }
            return 5;
        }

        private void TickReturnLights(int now)
        {
            if (_returnLights.Count == 0) { return; }

            var map = _comp.Pawn.Map;
            if (map == null || !_comp.Pawn.Spawned)
            {
                _returnLights.Clear();
                return;
            }

            var bossTarget = _comp.BodyCenter.Yto0();
            var hasBud = ApocryphaGimmicks.TryGetEverflowerBudPos(map, out var budTarget);
            var arrivedAtBoss = false;
            var arrivedAtBud = false;
            var budArrivalPos = budTarget;

            for (int i = _returnLights.Count - 1; i >= 0; i--)
            {
                var light = _returnLights[i];
                var target = ResolveTarget(light, bossTarget, hasBud, budTarget);
                var elapsed = now - light.startTick;
                var u = Mathf.Clamp01(elapsed / (float)Mathf.Max(1, light.durationTicks));

                light.prevPos = light.pos;
                light.pos = EvaluatePosition(light, target, u);

                if (elapsed >= light.durationTicks)
                {
                    if (light.toEverflower)
                    {
                        arrivedAtBud = true;
                        budArrivalPos = target;
                    }
                    else
                    {
                        arrivedAtBoss = true;
                    }

                    _returnLights.RemoveAt(i);
                    continue;
                }

                _returnLights[i] = light;

                if ((elapsed + i) % TrailSparkIntervalTicks == 0 && light.pos.ShouldSpawnMotesAt(map))
                {
                    var ember = FleckMaker.GetDataStatic(light.pos + Gen.RandomHorizontalVector(0.22f), map, VVFleckDefOf.VV_Fleck_ApocryphaEmber, Rand.Range(0.28f, 0.52f) * light.scale);
                    var backDir = (light.prevPos - light.pos).Yto0();
                    ember.velocityAngle = backDir.sqrMagnitude > 0.0001f ? backDir.AngleFlat() + Rand.Range(-30f, 30f) : Rand.Range(0f, 360f);
                    ember.velocitySpeed = Rand.Range(0.4f, 1.4f);
                    ember.instanceColor = Color.Lerp(GetLightColor(light), Color.white, Rand.Range(0.2f, 0.55f));
                    map.flecks.CreateFleck(ember);
                }
            }

            if (arrivedAtBoss && now - _lastBossArrivalBurstTick >= ArrivalBurstCooldownTicks)
            {
                _lastBossArrivalBurstTick = now;
                ThrowArrivalBurst(map, bossTarget, RecoverLightColor);
            }

            if (arrivedAtBud && now - _lastBudArrivalBurstTick >= ArrivalBurstCooldownTicks)
            {
                _lastBudArrivalBurstTick = now;
                ThrowArrivalBurst(map, budArrivalPos, DrainLightColor);
            }
        }

        private void SpawnReturnLight(int now, Vector3 start, Vector3 target, bool fromEverflower, bool toEverflower, float scale)
        {
            if (_returnLights.Count >= MaxReturnLights)
            {
                _returnLights.RemoveAt(0);
            }

            var startFlat = start.Yto0();
            var targetFlat = target.Yto0();
            var dist = (targetFlat - startFlat).magnitude;
            var isLongStream = fromEverflower || toEverflower;
            var baseTicks = Mathf.RoundToInt(dist / (ReturnCellsPerTick * Rand.Range(0.88f, 1.16f)));
            var durationTicks = Mathf.Clamp(baseTicks, isLongStream ? MinReturnDurationTicks : 38, MaxReturnDurationTicks);
            var arcSign = _nextArcSign;
            _nextArcSign = -_nextArcSign;
            var arcAmp = Mathf.Clamp(dist * (isLongStream ? 0.16f : 0.22f), isLongStream ? 1.4f : 1.1f, 6.2f) * Rand.Range(0.55f, 1.25f);
            var targetJitter = toEverflower ? TargetJitter * 0.65f : SourceJitter * 0.75f;

            var light = new ReturnFairyLight
            {
                startPos = startFlat,
                fixedTargetPos = targetFlat,
                pos = startFlat,
                prevPos = startFlat,
                targetOffset = Gen.RandomHorizontalVector(targetJitter),
                startTick = now,
                durationTicks = durationTicks,
                arcSign = arcSign,
                arcAmplitude = arcAmp,
                swirlFreq = Rand.Range(4.8f, 8.8f),
                swirlPhase = Rand.Range(0f, Mathf.PI * 2f),
                scale = scale,
                seed = Rand.Range(0f, 100f),
                fromEverflower = fromEverflower,
                toEverflower = toEverflower,
            };
            _returnLights.Add(light);
        }

        private static Vector3 ResolveTarget(in ReturnFairyLight light, Vector3 bossTarget, bool hasBud, Vector3 budTarget)
        {
            if (light.toEverflower)
            {
                return hasBud ? budTarget.Yto0() : light.fixedTargetPos;
            }

            return bossTarget;
        }

        private static Color GetLightColor(in ReturnFairyLight light)
        {
            if (light.fromEverflower) { return RecoverLightColor; }
            if (light.toEverflower) { return DrainLightColor; }
            return ViviReturnLightColor;
        }

        private static Vector3 EvaluatePosition(in ReturnFairyLight light, Vector3 target, float u)
        {
            var start = light.startPos;
            var goal = target + light.targetOffset * (1f - u * 0.6f);
            var chord = (goal - start).Yto0();
            var dist = chord.magnitude;
            var dir = dist > 0.001f ? chord / dist : Vector3.forward;
            var perp = new Vector3(-dir.z, 0f, dir.x);

            var smooth = u * u * (3f - 2f * u);
            var travel = Mathf.Lerp(smooth, u * u, 0.35f);
            var arch = Mathf.Sin(u * Mathf.PI);
            var arc = light.arcSign * light.arcAmplitude * arch * (1f - 0.3f * u);
            var sway = Mathf.Sin(u * light.swirlFreq + light.swirlPhase) * 0.45f * arch;
            var lift = arch * (light.fromEverflower || light.toEverflower ? 0.55f : 0.35f);

            var linear = Vector3.Lerp(start, goal, travel);
            return new Vector3(
                linear.x + perp.x * (arc + sway),
                0f,
                linear.z + perp.z * (arc + sway) + lift);
        }

        private static void ThrowDepartureBurst(Map map, Vector3 origin, Color color)
        {
            if (map == null || !origin.ShouldSpawnMotesAt(map)) { return; }

            if (VVFleckDefOf.VV_Fleck_GimmickPhaseRing != null)
            {
                var ring = FleckMaker.GetDataStatic(origin, map, VVFleckDefOf.VV_Fleck_GimmickPhaseRing, 1.35f);
                ring.instanceColor = color;
                map.flecks.CreateFleck(ring);
            }

            for (int i = 0; i < 6; i++)
            {
                var ember = FleckMaker.GetDataStatic(origin + Gen.RandomHorizontalVector(0.55f), map, VVFleckDefOf.VV_Fleck_ApocryphaEmber, Rand.Range(0.45f, 0.85f));
                ember.velocityAngle = Rand.Range(0f, 360f);
                ember.velocitySpeed = Rand.Range(1.2f, 3.0f);
                ember.instanceColor = Color.Lerp(color, Color.white, Rand.Range(0.2f, 0.5f));
                map.flecks.CreateFleck(ember);
            }
        }

        private static void ThrowArrivalBurst(Map map, Vector3 destination, Color color)
        {
            if (map == null || !destination.ShouldSpawnMotesAt(map)) { return; }

            if (VVFleckDefOf.VV_Fleck_GimmickPhaseRing != null)
            {
                var ring = FleckMaker.GetDataStatic(destination, map, VVFleckDefOf.VV_Fleck_GimmickPhaseRing, 0.95f);
                ring.instanceColor = Color.Lerp(color, Color.white, 0.35f);
                map.flecks.CreateFleck(ring);
            }

            for (int i = 0; i < 3; i++)
            {
                var ember = FleckMaker.GetDataStatic(destination + Gen.RandomHorizontalVector(0.45f), map, VVFleckDefOf.VV_Fleck_ApocryphaEmber, Rand.Range(0.35f, 0.65f));
                ember.velocityAngle = Rand.Range(0f, 360f);
                ember.velocitySpeed = Rand.Range(0.9f, 2.2f);
                ember.instanceColor = Color.Lerp(color, Color.white, Rand.Range(0.25f, 0.6f));
                map.flecks.CreateFleck(ember);
            }
        }
    }
}
