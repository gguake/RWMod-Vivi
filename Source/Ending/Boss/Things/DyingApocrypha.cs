using RimWorld;
using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace VVRace
{
    [StaticConstructorOnStartup]
    public class DyingApocrypha : Thing
    {
        public const int DissolveTicks = 450;
        public const int StolenTicks = 340;

        private const int ShakeInterval = 60;
        private const int BurstTicks = 54;
        private const float BurstPerTick = 6f;
        private static readonly FloatRange BurstDistance = new FloatRange(2.5f, 7f);
        private static readonly FloatRange BurstSpeed = new FloatRange(4f, 8f);
        private static readonly FloatRange BurstScale = new FloatRange(0.5f, 0.95f);
        private static readonly Color BurstColor = new Color(1f, 0.96f, 0.84f, 1f);
        private const float FlashDecay = 2.4f;
        private const float GlowDay = 0.2f;
        private const float GlowNight = 1f;
        private static readonly Color GlowColor = new Color(1f, 0.88f, 0.62f);
        private const float FlashPeak = 0.85f;

        private const float PetalsPerTexel = 0.28f;
        private const float SparksPerTexel = 0.12f;
        private const float PetalTrail = 0.025f;
        private const float PetalEdgeTint = 0.3f;
        private static readonly FloatRange PetalSpeed = new FloatRange(1.4f, 3.4f);
        private static readonly FloatRange PetalScale = new FloatRange(0.45f, 0.7f);
        private static readonly FloatRange SparkSpeed = new FloatRange(0.6f, 1.6f);
        private static readonly FloatRange SparkScale = new FloatRange(0.22f, 0.4f);
        private const float PetalSpreadAngle = 18f;

        private const float ReleaseWaveSecs = 2.8f;

        private const float FlowerDelaySecs = 0.15f;
        private const float FlowerDurationSecs = 2.2f;
        private const float StolenBossDelaySecs = 1.3f;
        private const float StolenBossDurationSecs = 3.2f;
        private const float DeathDelaySecs = 0.5f;
        private const float DeathDurationSecs = 4.8f;
        private const float SweepWeight = 0.85f;
        private const float DriftRiseCells = 0.9f;
        private const float DeathQuoteHeight = 2.2f;
        private const float DeathQuoteSecs = 5f;

        private const int SoulInterval = 2;
        private const float SoulLifeSecs = 2.2f;
        private const int SoulAbsorbInterval = 40;

        private static readonly DissolveStyle FlowerStyle = new DissolveStyle(FlowerDelaySecs, FlowerDurationSecs, new Color(1f, 0.93f, 0.72f, 1f), SweepWeight, 0f);
        private static readonly DissolveStyle StolenBossStyle = new DissolveStyle(StolenBossDelaySecs, StolenBossDurationSecs, new Color(1f, 0.62f, 0.88f, 1f), SweepWeight, 0f);
        private static readonly DissolveStyle DeathStyle = new DissolveStyle(DeathDelaySecs, DeathDurationSecs, new Color(1f, 0.95f, 0.8f, 1f), SweepWeight, 0f);
        private static readonly Color[] FlowerPetalColors =
        {
            new Color(1f, 0.97f, 0.86f, 0.9f),
            new Color(1f, 0.9f, 0.68f, 0.9f),
            new Color(1f, 0.94f, 0.78f, 0.85f),
        };
        private static readonly Color[] StolenPetalColors =
        {
            new Color(1f, 0.97f, 0.86f, 0.85f),
            new Color(1f, 0.9f, 0.68f, 0.85f),
            new Color(1f, 0.72f, 0.9f, 0.85f),
            new Color(1f, 0.5f, 0.84f, 0.8f),
        };

        private string _texPath;
        private Rot4 _rotation = Rot4.South;
        private float _drawSize = 8.5f;
        private Vector3 _drawOffset;
        private int _startTick = -1;
        private bool _notified;

        private bool _questBoss = true;

        private bool _stolen;
        private string _flowerTexPath;
        private Vector3 _flowerDrawPos;
        private Vector2 _flowerDrawSize;
        private Vector3 _flowerBudPos;
        private Vector3 _bodyOffset;
        private Vector3 _wind = Vector3.forward;

        private int _waveTick = -1;
        private Vector3 _waveOrigin;
        private float _waveHalfSize;
        private List<Pawn> _waveReached = new List<Pawn>();

        private float _petalThreshold = -1f;
        private float _petalBudget;
        private float _sparkBudget;

        private float AgeSecs => Mathf.Max(0, Find.TickManager.TicksGame - _startTick) / 60f;

        private float FlowerProgress => Mathf.Clamp01((AgeSecs - FlowerDelaySecs) / FlowerDurationSecs);

        private DissolveStyle BossStyle => _stolen ? StolenBossStyle : DeathStyle;

        private bool BossFlip => _rotation == Rot4.West;

        private Vector2 BossUvSweep => new Vector2(BossFlip ? -_wind.x : _wind.x, _wind.z);

        private DissolveMask BossMask => _texPath.NullOrEmpty() ? null : DissolveMask.Get(_texPath + DissolveRenderer.RotationSuffix(_rotation));

        private float BossFlash => _stolen ? 0f : FlashPeak * Mathf.Exp(-AgeSecs * FlashDecay);

        public bool ReleaseWaveActive => _waveTick >= 0;

        public static float ReleaseWaveDurationSecs => ReleaseWaveSecs;

        private float BossProgress => Mathf.Clamp01((AgeSecs - BossStyle.delaySecs) / BossStyle.durationSecs);

        private DissolveStyle ShaderStyle
        {
            get
            {
                var style = BossStyle;
                var mask = BossMask;
                if (mask == null || !mask.TryGetKeyRange(BossUvSweep, SweepWeight, out var min, out var max) || max - min < 0.05f) { return style; }

                var scale = 1f + DissolveMask.EdgeWidth;
                var duration = style.durationSecs * scale / (max - min);
                var delay = style.delaySecs - min / scale * duration;
                return new DissolveStyle(delay, duration, style.edgeColor, style.sweepWeight, style.ashAmount);
            }
        }

        private float DissolveThreshold
        {
            get
            {
                var style = ShaderStyle;
                return DissolveMask.Threshold(Mathf.Clamp01((AgeSecs - style.delaySecs) / style.durationSecs));
            }
        }

        private Vector3 Drift
        {
            get
            {
                var p = BossProgress;
                p *= p;
                return new Vector3(_wind.x * 0.4f * p, 0f, DriftRiseCells * p);
            }
        }

        public void InitFrom(Pawn boss)
        {
            var comp = boss.TryGetComp<CompApocrypha>();
            var graphicData = boss.ageTracker?.CurKindLifeStage?.bodyGraphicData;

            _texPath = graphicData?.texPath;
            _rotation = boss.Rotation;
            _drawSize = graphicData?.drawSize.x ?? 8.5f;
            _questBoss = comp != null && comp.IsQuestBossFight;
            _bodyOffset = ApocryphaGimmicks.GetBossBodyOffset(boss);
            _drawOffset = EndingVfxUtility.OffsetFromCell(boss.DrawPos, boss.Position);

            var bossCenter = boss.DrawPos + _bodyOffset;
            if (ApocryphaGimmicks.TryGetEverflowerBudPos(ViviEndingQuest.Current?.AnchorMap, out var bud))
            {
                _flowerBudPos = bud;
                _wind = EndingVfxUtility.SafeNormalizeHorizontal(bud - bossCenter, Vector3.forward, minSqrMagnitude: 0.25f);
            }
        }

        public void InitStolen(Pawn boss, ArcanePlant_Everflower everflower)
        {
            InitFrom(boss);
            _stolen = true;
            _questBoss = false;

            var bossCenter = boss.DrawPos + _bodyOffset;
            var budPos = bossCenter;
            var flowerComp = everflower?.EverflowerComp;
            var graphicData = flowerComp?.CurrentGraphicData;
            if (graphicData != null)
            {
                _flowerTexPath = graphicData.texPath;
                _flowerDrawPos = everflower.DrawPos + graphicData.drawOffset;
                _flowerDrawSize = graphicData.drawSize;
                budPos = ApocryphaGimmicks.EverflowerBudPos(everflower);
            }
            _flowerBudPos = budPos;

            var away = EndingVfxUtility.SafeNormalizeHorizontal(bossCenter - budPos, Vector3.right, minSqrMagnitude: 0.25f);
            _wind = (away * 0.75f + Vector3.forward * 0.55f).normalized;
        }

        public override void SpawnSetup(Map map, bool respawningAfterLoad)
        {
            base.SpawnSetup(map, respawningAfterLoad);

            if (respawningAfterLoad || _startTick >= 0) { return; }

            _startTick = Find.TickManager.TicksGame;
            if (_stolen) { return; }

            VVEffecterDefOf.VV_Effecter_ApocryphaDeath.Spawn(Position, map, _drawOffset + _bodyOffset).Cleanup();
            ApocryphaUtility.ShakeCamera(map, 2f);
            StartReleaseWave(map);

            if (!_questBoss) { return; }

            var quote = LocalizeString_Etc.VV_Speech_AsphodelDeath.Translate();
            var quotePos = DrawPos + _drawOffset + _bodyOffset + new Vector3(0f, 0f, DeathQuoteHeight);
            MoteMaker.ThrowText(quotePos, map, quote, DeathQuoteSecs);
            Find.LetterStack.ReceiveLetter(
                LocalizeString_Letter.VV_Letter_AsphodelFallenLabel.Translate(),
                LocalizeString_Letter.VV_Letter_AsphodelFallen.Translate(quote.Named("QUOTE")),
                LetterDefOf.PositiveEvent,
                new LookTargets(Position, map));
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref _texPath, "texPath");
            Scribe_Values.Look(ref _rotation, "rotation", Rot4.South);
            Scribe_Values.Look(ref _drawSize, "drawSize", 8.5f);
            Scribe_Values.Look(ref _drawOffset, "drawOffset", Vector3.zero);
            Scribe_Values.Look(ref _startTick, "startTick", -1);
            Scribe_Values.Look(ref _notified, "notified", false);
            Scribe_Values.Look(ref _questBoss, "questBoss", false);
            Scribe_Values.Look(ref _stolen, "stolen", false);
            Scribe_Values.Look(ref _flowerTexPath, "flowerTexPath");
            Scribe_Values.Look(ref _flowerDrawPos, "flowerDrawPos", Vector3.zero);
            Scribe_Values.Look(ref _flowerDrawSize, "flowerDrawSize", Vector2.zero);
            Scribe_Values.Look(ref _flowerBudPos, "flowerBudPos", Vector3.zero);
            Scribe_Values.Look(ref _bodyOffset, "bodyOffset", Vector3.zero);
            Scribe_Values.Look(ref _wind, "wind", Vector3.forward);
            Scribe_Values.Look(ref _waveTick, "waveTick", -1);
            Scribe_Values.Look(ref _waveOrigin, "waveOrigin", Vector3.zero);
            Scribe_Values.Look(ref _waveHalfSize, "waveHalfSize", 0f);
            Scribe_Collections.Look(ref _waveReached, "waveReached", LookMode.Reference);

            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                _waveReached ??= new List<Pawn>();
                _waveReached.RemoveAll(v => v == null);
            }
        }

        protected override void Tick()
        {
            base.Tick();
            if (!Spawned) { return; }

            var now = Find.TickManager.TicksGame;
            TickReleaseWave(now);

            var age = now - _startTick;
            if (age >= (_stolen ? StolenTicks : DissolveTicks))
            {
                Complete();
                return;
            }

            if (_stolen)
            {
                ThrowFlowerPetals();
            }
            else if (age < BurstTicks)
            {
                ThrowLightBurst(age);
            }

            ThrowBossPetals(age);
        }

        private void ThrowFlowerPetals()
        {
            var flowerProgress = FlowerProgress;
            if (flowerProgress <= 0f || flowerProgress >= 1f || !_flowerBudPos.ShouldSpawnMotesAt(Map)) { return; }

            var bossCenter = DrawPos + _drawOffset + _bodyOffset + Drift;
            for (int i = 0; i < 2; i++)
            {
                var pos = _flowerBudPos + new Vector3(Rand.Range(-1.6f, 1.6f), 0f, Rand.Range(-1.4f, 1.2f));
                var toBoss = (bossCenter - pos).Yto0();
                ThrowPetal(pos, toBoss.AngleFlat() + Rand.Range(-10f, 10f), toBoss.magnitude / Rand.Range(1f, 1.5f), Rand.Range(0.5f, 0.9f), FlowerPetalColors.RandomElement());
            }
        }

        private void ThrowLightBurst(int age)
        {
            var map = Map;
            var center = DrawPos + _drawOffset + _bodyOffset;
            var bodyPos = DrawPos + _drawOffset + Drift;
            var mask = BossMask;
            var threshold = DissolveThreshold;
            var count = GenMath.RoundRandom(BurstPerTick * (1f - age / (float)BurstTicks));
            for (int i = 0; i < count; i++)
            {
                Vector3 from;
                if (mask != null && mask.TrySampleRemaining(threshold, BossUvSweep, SweepWeight, out var uv, out _))
                {
                    from = DissolveMask.ToWorld(uv, bodyPos, new Vector2(_drawSize, _drawSize), BossFlip);
                }
                else
                {
                    from = center + Gen.RandomHorizontalVector(0.8f);
                }

                var dir = EndingVfxUtility.SafeNormalizeHorizontal(from - center, Vector3Utility.FromAngleFlat(Rand.Range(0f, 360f)));
                ApocryphaGimmicks.ThrowFairyLight(map, from, from + dir * BurstDistance.RandomInRange, BurstSpeed.RandomInRange, BurstScale.RandomInRange, BurstColor);
            }
        }

        private void ThrowBossPetals(int age)
        {
            var map = Map;
            var bossProgress = BossProgress;
            var center = DrawPos + _drawOffset + Drift;
            if (bossProgress <= 0f || !center.ShouldSpawnMotesAt(map))
            {
                _petalThreshold = -1f;
                return;
            }

            var threshold = DissolveThreshold;
            var from = _petalThreshold < 0f ? threshold - 0.004f : _petalThreshold;
            _petalThreshold = threshold;
            if (from >= 1f) { return; }

            if (age % ShakeInterval == 0)
            {
                ApocryphaUtility.ShakeCamera(map, 0.2f);
            }

            var colors = _stolen ? StolenPetalColors : FlowerPetalColors;
            var mask = BossMask;
            var size = new Vector2(_drawSize, _drawSize);
            var windAngle = _wind.AngleFlat();
            if (mask == null)
            {
                ThrowFrontPetals(center, bossProgress, windAngle, colors);
            }
            else
            {
                var sweep = BossUvSweep;
                var crossed = mask.CountBetween(from, threshold, sweep, SweepWeight);
                _petalBudget += crossed * PetalsPerTexel * (_stolen ? 0.7f : 1f);
                _sparkBudget += crossed * SparksPerTexel;
                var edgeColor = BossStyle.edgeColor;
                while (_petalBudget >= 1f)
                {
                    _petalBudget -= 1f;
                    if (!mask.TrySample(from - PetalTrail, threshold, sweep, SweepWeight, out var uv, out var texel)) { break; }

                    var color = Color.Lerp(texel, Color.Lerp(edgeColor, colors.RandomElement(), 0.5f), PetalEdgeTint);
                    color.a = Rand.Range(0.6f, 0.8f);
                    var angle = windAngle + Rand.Range(-PetalSpreadAngle, PetalSpreadAngle);
                    ThrowPetal(DissolveMask.ToWorld(uv, center, size, BossFlip), angle, PetalSpeed.RandomInRange * (_stolen ? 0.8f : 1f), PetalScale.RandomInRange, color, angle);
                }

                while (_sparkBudget >= 1f)
                {
                    _sparkBudget -= 1f;
                    if (!mask.TrySample(threshold, threshold + DissolveMask.EdgeWidth, sweep, SweepWeight, out var uv, out _)) { break; }

                    var data = FleckMaker.GetDataStatic(DissolveMask.ToWorld(uv, center, size, BossFlip), map, VVFleckDefOf.VV_Fleck_ApocryphaEmber, SparkScale.RandomInRange);
                    data.velocityAngle = windAngle + Rand.Range(-30f, 30f);
                    data.velocitySpeed = SparkSpeed.RandomInRange;
                    data.instanceColor = edgeColor;
                    map.flecks.CreateFleck(data);
                }
            }

            var assimilation = Hediff_EverflowerAssimilation.Of(AbsorbingResonator);
            if (assimilation == null || age % SoulInterval != 0) { return; }

            var target = assimilation.BodyPoint;
            Vector3 soulPos;
            if (mask != null && mask.TrySample(from - PetalTrail, threshold + DissolveMask.EdgeWidth, BossUvSweep, SweepWeight, out var soulUv, out _))
            {
                soulPos = DissolveMask.ToWorld(soulUv, center, size, BossFlip);
            }
            else
            {
                soulPos = center + _wind * (_drawSize * 0.8f * (0.5f - bossProgress)) + EndingVfxUtility.HorizontalPerpendicular(_wind) * Rand.Range(-_drawSize * 0.3f, _drawSize * 0.3f);
            }
            ManifestationRenderer.ThrowConverging(map, VVFleckDefOf.VV_Fleck_AssimilationSoul, soulPos, target, SoulLifeSecs, Rand.Range(0.45f, 0.8f), null, alignToVelocity: false);

            if (age % SoulAbsorbInterval == 0 && age >= SoulLifeSecs * 60f)
            {
                map.flecks.CreateFleck(FleckMaker.GetDataStatic(target, map, VVFleckDefOf.VV_Fleck_AssimilationMote, Rand.Range(1.2f, 1.6f)));
            }
        }

        private void ThrowFrontPetals(Vector3 center, float progress, float windAngle, Color[] colors)
        {
            if (progress >= 1f) { return; }

            var front = _drawSize * 0.8f * (0.5f - progress);
            var side = EndingVfxUtility.HorizontalPerpendicular(_wind);
            var halfWidth = _drawSize * 0.3f;
            var count = _stolen ? 2 : 3;
            for (int i = 0; i < count; i++)
            {
                var pos = center + _wind * (front + Rand.Range(-0.3f, 0.3f)) + side * Rand.Range(-halfWidth, halfWidth);
                var angle = windAngle + Rand.Range(-25f, 25f);
                ThrowPetal(pos, angle, PetalSpeed.RandomInRange, PetalScale.RandomInRange, colors.RandomElement(), angle);
            }
        }

        private void StartReleaseWave(Map map)
        {
            _waveTick = _startTick;
            _waveOrigin = (DrawPos + _drawOffset + _bodyOffset).Yto0();
            _waveHalfSize = EndingVfxUtility.ShockWaveHalfSize(EndingVfxUtility.MaxDistanceToMapCorner(map, _waveOrigin));
            _waveReached.Clear();
            EndingVfxUtility.SpawnShockWave(map, _waveOrigin, VVFleckDefOf.VV_Fleck_ApocryphaReleaseWave, _waveHalfSize);
        }

        private void TickReleaseWave(int now)
        {
            if (_waveTick < 0) { return; }

            var age = now - _waveTick;
            var map = Map;
            var pawns = map.mapPawns.AllPawnsSpawned;
            for (int i = 0; i < pawns.Count; i++)
            {
                var pawn = pawns[i];
                if (pawn.Dead || !pawn.IsVivi() || _waveReached.Contains(pawn)) { continue; }

                var distance = (pawn.Position.ToVector3Shifted() - _waveOrigin).MagnitudeHorizontal();
                if (age < EndingVfxUtility.ShockWaveArrivalTicks(distance, _waveHalfSize, ReleaseWaveSecs)) { continue; }

                _waveReached.Add(pawn);
                Refresh(pawn, map);
            }

            if (age >= Mathf.CeilToInt(ReleaseWaveSecs * 60f))
            {
                _waveTick = -1;
                _waveReached.Clear();
            }
        }

        private static void Refresh(Pawn pawn, Map map)
        {
            ApocryphaUtility.HealNonPermanentInjuries(pawn);

            var needs = pawn.needs;
            if (needs?.food != null)
            {
                needs.food.CurLevel = needs.food.MaxLevel;
            }
            if (needs?.rest != null)
            {
                needs.rest.CurLevel = needs.rest.MaxLevel;
            }

            EndingVfxUtility.PlayEffectAt(map, VVEffecterDefOf.VV_Effecter_ApocryphaReleaseRefresh, pawn.DrawPos);
        }

        private Pawn AbsorbingResonator
        {
            get
            {
                if (!_questBoss || _stolen) { return null; }

                var pawn = ViviEndingQuest.Current?.AssimilatedPawn;
                return pawn != null && pawn.MapHeld == Map ? pawn : null;
            }
        }

        private void ThrowPetal(Vector3 pos, float angle, float speed, float scale, Color color, float? facing = null)
        {
            var data = FleckMaker.GetDataStatic(pos, Map, VVFleckDefOf.VV_Fleck_ApocryphaStealPetal, scale);
            data.velocityAngle = angle;
            data.velocitySpeed = speed;
            data.rotation = facing.HasValue ? facing.Value + Rand.Range(-25f, 25f) : Rand.Range(0f, 360f);
            data.rotationRate = facing.HasValue ? Rand.Range(-70f, 70f) : Rand.Range(-240f, 240f);
            data.instanceColor = color;
            Map.flecks.CreateFleck(data);
        }

        private void Complete()
        {
            if (!_stolen)
            {
                VVEffecterDefOf.VV_Effecter_ApocryphaVanish.Spawn(Position, Map, 2f).Cleanup();

                var assimilation = Hediff_EverflowerAssimilation.Of(AbsorbingResonator);
                if (assimilation != null)
                {
                    EndingVfxUtility.PlayEffectAt(Map, VVEffecterDefOf.VV_Effecter_AssimilationAbsorb, assimilation.BodyPoint);
                }
            }

            NotifyDefeated();

            if (!Destroyed)
            {
                Destroy();
            }
        }

        public override void Destroy(DestroyMode mode = DestroyMode.Vanish)
        {
            NotifyDefeated();
            base.Destroy(mode);
        }

        private void NotifyDefeated()
        {
            if (_notified) { return; }

            _notified = true;
            if (!_questBoss) { return; }

            ApocryphaUtility.ReleaseAsphodelBound(MapHeld, PositionHeld);
            ViviEndingQuest.Current?.Notify_BossDefeated();
        }

        protected override void DrawAt(Vector3 drawLoc, bool flip = false)
        {
            if (_stolen)
            {
                DrawStolenFlower();
            }

            if (_texPath.NullOrEmpty()) { return; }

            var pos = drawLoc + _drawOffset + Drift;
            pos.y = AltitudeLayer.Pawn.AltitudeFor();

            var size = new Vector2(_drawSize, _drawSize);
            var style = ShaderStyle;
            DissolveRenderer.Draw(DissolveRenderer.GetMaterial(_texPath, _rotation), pos, size, BossFlip, AgeSecs, style, WindSweep, BossFlash);
            if (_stolen) { return; }

            var dark = 1f - Mathf.Clamp01(Map.glowGrid.GroundGlowAt(Position));
            pos.y += 0.02f;
            DissolveRenderer.Draw(DissolveRenderer.GetGlowMaterial(_texPath, _rotation), pos, size, BossFlip, AgeSecs, style, WindSweep, 0f, GlowColor * Mathf.Lerp(GlowDay, GlowNight, dark));
        }

        private void DrawStolenFlower()
        {
            if (_flowerTexPath.NullOrEmpty() || FlowerProgress >= 1f) { return; }

            DissolveRenderer.Draw(DissolveRenderer.GetMaterial(_flowerTexPath), _flowerDrawPos, _flowerDrawSize, false, AgeSecs, FlowerStyle, WindSweep);
        }

        private Vector2 WindSweep => new Vector2(_wind.x, _wind.z);
    }
}
