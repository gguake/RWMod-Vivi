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

        private const float DissolveDelaySecs = 0.6f;
        private const float DissolveDurationSecs = 6.4f;
        private const int AshInterval = 2;
        private const int EmberInterval = 3;
        private const int ShakeInterval = 60;

        private const int SoulInterval = 2;
        private const float SoulLifeSecs = 2.2f;
        private const int SoulAbsorbInterval = 40;

        private const float FlowerDelaySecs = 0.15f;
        private const float FlowerDurationSecs = 2.2f;
        private const float StolenBossDelaySecs = 1.3f;
        private const float StolenBossDurationSecs = 3.8f;
        private const float StolenSweepWeight = 0.85f;
        private const float StolenRiseCells = 0.9f;
        private const float DeathAshAmount = 0.75f;

        private static readonly FloatRange AshScale = new FloatRange(1f, 1.9f);
        private static readonly FloatRange AshSpeed = new FloatRange(0.4f, 1.1f);
        private static readonly FloatRange EmberScale = new FloatRange(0.25f, 0.6f);
        private static readonly FloatRange EmberSpeed = new FloatRange(0.8f, 2f);

        private static readonly DissolveStyle DeathStyle = new DissolveStyle(DissolveDelaySecs, DissolveDurationSecs, new Color(1f, 0.78f, 0.35f, 1f), 0f, DeathAshAmount);
        private static readonly DissolveStyle FlowerStyle = new DissolveStyle(FlowerDelaySecs, FlowerDurationSecs, new Color(1f, 0.93f, 0.72f, 1f), StolenSweepWeight, 0f);
        private static readonly DissolveStyle StolenBossStyle = new DissolveStyle(StolenBossDelaySecs, StolenBossDurationSecs, new Color(1f, 0.62f, 0.88f, 1f), StolenSweepWeight, 0f);
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

        private float DissolveProgress => Mathf.Clamp01((AgeSecs - DissolveDelaySecs) / DissolveDurationSecs);

        private float FlowerProgress => Mathf.Clamp01((AgeSecs - FlowerDelaySecs) / FlowerDurationSecs);
        private float StolenBossProgress => Mathf.Clamp01((AgeSecs - StolenBossDelaySecs) / StolenBossDurationSecs);

        private Vector3 StolenDrift
        {
            get
            {
                var p = StolenBossProgress;
                p *= p;
                return new Vector3(_wind.x * 0.4f * p, 0f, StolenRiseCells * p);
            }
        }

        public void InitFrom(Pawn boss, Rot4? rotationOverride = null, Vector3? drawOffsetOverride = null)
        {
            var comp = boss.TryGetComp<CompApocrypha>();
            var graphicData = boss.ageTracker?.CurKindLifeStage?.bodyGraphicData;

            _texPath = graphicData?.texPath;
            _rotation = rotationOverride ?? boss.Rotation;
            _drawSize = comp?.CurrentVisualDrawSize ?? graphicData?.drawSize.x ?? 8.5f;
            _questBoss = comp != null && comp.IsQuestBossFight;

            _drawOffset = drawOffsetOverride ?? EndingVfxUtility.OffsetFromCell(boss.DrawPos, boss.Position);
        }

        public void InitStolen(Pawn boss, ArcanePlant_Everflower everflower)
        {
            InitFrom(boss);
            _stolen = true;
            _questBoss = false;
            _bodyOffset = ApocryphaGimmicks.GetBossBodyOffset(boss);

            var bossCenter = boss.DrawPos + _bodyOffset;
            var budPos = bossCenter;
            var flowerComp = everflower?.EverflowerComp;
            var graphicData = flowerComp?.CurrentGraphicData;
            if (graphicData != null)
            {
                _flowerTexPath = graphicData.texPath;
                _flowerDrawPos = everflower.DrawPos + graphicData.drawOffset;
                _flowerDrawSize = graphicData.drawSize;
                budPos = everflower.DrawPos + flowerComp.RitualEffecterOffset;
            }
            _flowerBudPos = budPos;

            var away = EndingVfxUtility.SafeNormalizeHorizontal(bossCenter - budPos, Vector3.right, minSqrMagnitude: 0.25f);
            _wind = (away * 0.75f + Vector3.forward * 0.55f).normalized;
        }

        public override void SpawnSetup(Map map, bool respawningAfterLoad)
        {
            base.SpawnSetup(map, respawningAfterLoad);

            if (!respawningAfterLoad && _startTick < 0)
            {
                _startTick = Find.TickManager.TicksGame;

                if (!_stolen)
                {
                    VVEffecterDefOf.VV_Effecter_ApocryphaDeath.Spawn(Position, map, _drawOffset).Cleanup();
                    ApocryphaUtility.ShakeCamera(map, 2f);
                }
            }
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
                ThrowStolenPetals(age);
            }
            else
            {
                ThrowDissolveParticles(age);
            }
        }

        private void ThrowStolenPetals(int age)
        {
            var map = Map;
            var bossCenter = DrawPos + _drawOffset + _bodyOffset + StolenDrift;

            var flowerProgress = FlowerProgress;
            if (flowerProgress > 0f && flowerProgress < 1f && _flowerBudPos.ShouldSpawnMotesAt(map))
            {
                for (int i = 0; i < 2; i++)
                {
                    var pos = _flowerBudPos + new Vector3(Rand.Range(-1.6f, 1.6f), 0f, Rand.Range(-1.4f, 1.2f));
                    var toBoss = bossCenter - pos;
                    toBoss.y = 0f;
                    ThrowPetal(pos, toBoss.AngleFlat() + Rand.Range(-10f, 10f), toBoss.magnitude / Rand.Range(1f, 1.5f), Rand.Range(0.5f, 0.9f), FlowerPetalColors);
                }
            }

            var bossProgress = StolenBossProgress;
            if (bossProgress <= 0f || bossProgress >= 1f || !bossCenter.ShouldSpawnMotesAt(map)) { return; }

            if (age % ShakeInterval == 0)
            {
                ApocryphaUtility.ShakeCamera(map, 0.2f);
            }

            var center = DrawPos + _drawOffset + StolenDrift;
            var front = _drawSize * 0.8f * (0.5f - bossProgress);
            var side = EndingVfxUtility.HorizontalPerpendicular(_wind);
            var halfWidth = _drawSize * 0.3f;
            var windAngle = _wind.AngleFlat();
            for (int i = 0; i < 2; i++)
            {
                var pos = center + _wind * (front + Rand.Range(-0.3f, 0.3f)) + side * Rand.Range(-halfWidth, halfWidth);
                ThrowPetal(pos, windAngle + Rand.Range(-25f, 25f), Rand.Range(1.4f, 3.4f), Rand.Range(0.45f, 0.85f), StolenPetalColors);
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

        private void ThrowDissolveParticles(int age)
        {
            var progress = DissolveProgress;
            if (progress <= 0f || progress >= 1f) { return; }

            var center = DrawPos + _drawOffset;
            if (!center.ShouldSpawnMotesAt(Map)) { return; }

            if (age % ShakeInterval == 0)
            {
                ApocryphaUtility.ShakeCamera(Map, 0.3f);
            }

            var frontZ = center.z + _drawSize * (0.42f - 0.8f * progress);
            var halfWidth = _drawSize * 0.32f;

            DissolveRenderer.ThrowAshAndEmbers(
                Map, center, frontZ, halfWidth, age, AshInterval, EmberInterval,
                ashJitterZ: 0.3f, AshScale, AshSpeed,
                emberJitterZ: 0.2f, EmberScale, EmberSpeed);

            ThrowSoulsToResonator(age, center, frontZ, halfWidth);
        }

        private Pawn AbsorbingResonator
        {
            get
            {
                if (!_questBoss) { return null; }

                var pawn = ViviEndingQuest.Current?.AssimilatedPawn;
                return pawn != null && pawn.MapHeld == Map ? pawn : null;
            }
        }

        private void ThrowSoulsToResonator(int age, Vector3 center, float frontZ, float halfWidth)
        {
            if (age % SoulInterval != 0) { return; }

            var resonator = AbsorbingResonator;
            var assimilation = Hediff_EverflowerAssimilation.Of(resonator);
            if (assimilation == null) { return; }

            var target = assimilation.BodyPoint;
            var pos = new Vector3(center.x + Rand.Range(-halfWidth, halfWidth), center.y, frontZ + Rand.Range(-0.3f, 0.3f));
            ManifestationRenderer.ThrowConverging(Map, VVFleckDefOf.VV_Fleck_AssimilationSoul, pos, target, SoulLifeSecs, Rand.Range(0.45f, 0.8f), null, alignToVelocity: false);

            if (age % SoulAbsorbInterval == 0 && age >= SoulLifeSecs * 60f)
            {
                var pulse = FleckMaker.GetDataStatic(target, Map, VVFleckDefOf.VV_Fleck_AssimilationMote, Rand.Range(1.2f, 1.6f));
                Map.flecks.CreateFleck(pulse);
            }
        }

        private void Complete()
        {
            var map = Map;
            if (!_stolen)
            {
                FilthMaker.TryMakeFilth(Position, map, ThingDefOf.Filth_Ash, 3);
                VVEffecterDefOf.VV_Effecter_ApocryphaVanish.Spawn(Position, map, 2f).Cleanup();

                var assimilation = Hediff_EverflowerAssimilation.Of(AbsorbingResonator);
                if (assimilation != null)
                {
                    EndingVfxUtility.PlayEffectAt(map, VVEffecterDefOf.VV_Effecter_AssimilationAbsorb, assimilation.BodyPoint);
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
            if (_questBoss)
            {
                ViviEndingQuest.Current?.Notify_BossDefeated();
            }
        }

        protected override void DrawAt(Vector3 drawLoc, bool flip = false)
        {
            if (_stolen)
            {
                DrawStolenFlower();
            }

            if (_texPath.NullOrEmpty()) { return; }

            var pos = drawLoc + _drawOffset;
            pos.y = AltitudeLayer.Pawn.AltitudeFor();

            var material = DissolveRenderer.GetMaterial(_texPath, _rotation);
            var size = new Vector2(_drawSize, _drawSize);
            var westFlip = _rotation == Rot4.West;
            if (_stolen)
            {
                DissolveRenderer.Draw(material, pos + StolenDrift, size, westFlip, AgeSecs, StolenBossStyle, WindSweep);
            }
            else
            {
                DissolveRenderer.Draw(material, pos, size, westFlip, AgeSecs, DeathStyle, Vector2.right);
            }
        }

        private void DrawStolenFlower()
        {
            if (_flowerTexPath.NullOrEmpty() || FlowerProgress >= 1f) { return; }

            DissolveRenderer.Draw(DissolveRenderer.GetMaterial(_flowerTexPath), _flowerDrawPos, _flowerDrawSize, false, AgeSecs, FlowerStyle, WindSweep);
        }

        private Vector2 WindSweep => new Vector2(_wind.x, _wind.z);
    }
}
