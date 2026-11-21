using RimWorld;
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
        private const int BurstTicks = 40;
        private const int BurstPerTick = 5;
        private static readonly FloatRange BurstDistance = new FloatRange(4f, 9f);
        private static readonly FloatRange BurstSpeed = new FloatRange(5f, 9f);
        private static readonly FloatRange BurstScale = new FloatRange(0.6f, 1.1f);
        private static readonly Color BurstColor = new Color(1f, 0.96f, 0.84f, 1f);

        private const float FlowerDelaySecs = 0.15f;
        private const float FlowerDurationSecs = 2.2f;
        private const float StolenBossDelaySecs = 1.3f;
        private const float StolenBossDurationSecs = 3.8f;
        private const float DeathDelaySecs = 1.1f;
        private const float DeathDurationSecs = 5.6f;
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

        private float AgeSecs => Mathf.Max(0, Find.TickManager.TicksGame - _startTick) / 60f;

        private float FlowerProgress => Mathf.Clamp01((AgeSecs - FlowerDelaySecs) / FlowerDurationSecs);

        private DissolveStyle BossStyle => _stolen ? StolenBossStyle : DeathStyle;

        private float BossProgress => Mathf.Clamp01((AgeSecs - BossStyle.delaySecs) / BossStyle.durationSecs);

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
        }

        protected override void Tick()
        {
            base.Tick();
            if (!Spawned) { return; }

            var age = Find.TickManager.TicksGame - _startTick;
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
                ThrowLightBurst();
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
                ThrowPetal(pos, toBoss.AngleFlat() + Rand.Range(-10f, 10f), toBoss.magnitude / Rand.Range(1f, 1.5f), Rand.Range(0.5f, 0.9f), FlowerPetalColors);
            }
        }

        private void ThrowLightBurst()
        {
            var map = Map;
            var center = DrawPos + _drawOffset + _bodyOffset;
            for (int i = 0; i < BurstPerTick; i++)
            {
                var dir = Quaternion.AngleAxis(Rand.Range(0f, 360f), Vector3.up) * Vector3.forward;
                var from = center + dir * Rand.Range(0.2f, 0.8f);
                ApocryphaGimmicks.ThrowFairyLight(map, from, from + dir * BurstDistance.RandomInRange, BurstSpeed.RandomInRange, BurstScale.RandomInRange, BurstColor);
            }
        }

        private void ThrowBossPetals(int age)
        {
            var map = Map;
            var bossProgress = BossProgress;
            var center = DrawPos + _drawOffset + Drift;
            if (bossProgress <= 0f || bossProgress >= 1f || !center.ShouldSpawnMotesAt(map)) { return; }

            if (age % ShakeInterval == 0)
            {
                ApocryphaUtility.ShakeCamera(map, 0.2f);
            }

            var front = _drawSize * 0.8f * (0.5f - bossProgress);
            var side = EndingVfxUtility.HorizontalPerpendicular(_wind);
            var halfWidth = _drawSize * 0.3f;
            var windAngle = _wind.AngleFlat();
            var count = _stolen ? 2 : 3;
            for (int i = 0; i < count; i++)
            {
                var pos = center + _wind * (front + Rand.Range(-0.3f, 0.3f)) + side * Rand.Range(-halfWidth, halfWidth);
                ThrowPetal(pos, windAngle + Rand.Range(-25f, 25f), _stolen ? Rand.Range(1.4f, 3.4f) : Rand.Range(2f, 4.6f), Rand.Range(0.45f, 0.85f), _stolen ? StolenPetalColors : FlowerPetalColors);
            }

            var assimilation = Hediff_EverflowerAssimilation.Of(AbsorbingResonator);
            if (assimilation == null || age % SoulInterval != 0) { return; }

            var target = assimilation.BodyPoint;
            var soulPos = center + _wind * front + side * Rand.Range(-halfWidth, halfWidth);
            ManifestationRenderer.ThrowConverging(map, VVFleckDefOf.VV_Fleck_AssimilationSoul, soulPos, target, SoulLifeSecs, Rand.Range(0.45f, 0.8f), null, alignToVelocity: false);

            if (age % SoulAbsorbInterval == 0 && age >= SoulLifeSecs * 60f)
            {
                map.flecks.CreateFleck(FleckMaker.GetDataStatic(target, map, VVFleckDefOf.VV_Fleck_AssimilationMote, Rand.Range(1.2f, 1.6f)));
            }
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

        private void ThrowPetal(Vector3 pos, float angle, float speed, float scale, Color[] colors)
        {
            var data = FleckMaker.GetDataStatic(pos, Map, VVFleckDefOf.VV_Fleck_ApocryphaStealPetal, scale);
            data.velocityAngle = angle;
            data.velocitySpeed = speed;
            data.rotation = Rand.Range(0f, 360f);
            data.rotationRate = Rand.Range(-240f, 240f);
            data.instanceColor = colors.RandomElement();
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

            DissolveRenderer.Draw(DissolveRenderer.GetMaterial(_texPath, _rotation), pos, new Vector2(_drawSize, _drawSize), _rotation == Rot4.West, AgeSecs, BossStyle, WindSweep);
        }

        private void DrawStolenFlower()
        {
            if (_flowerTexPath.NullOrEmpty() || FlowerProgress >= 1f) { return; }

            DissolveRenderer.Draw(DissolveRenderer.GetMaterial(_flowerTexPath), _flowerDrawPos, _flowerDrawSize, false, AgeSecs, FlowerStyle, WindSweep);
        }

        private Vector2 WindSweep => new Vector2(_wind.x, _wind.z);
    }
}
