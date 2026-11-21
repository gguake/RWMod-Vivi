using RimWorld;
using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace VVRace
{
    [StaticConstructorOnStartup]
    public class ApocryphaRebirth : ThingWithComps, IThingHolder
    {
        public const int BurnTicks = 180;
        public const int GrowStartTick = 120;
        public const int MorphStartTick = 360;
        public const int FirstQuoteTick = 130;
        public const int SecondQuoteTick = 245;

        private const float BurnDelaySecs = 0.15f;
        private const float BurnDurationSecs = 2.6f;
        private const int AshInterval = 1;
        private const int EmberInterval = 2;
        private const int GlintInterval = 4;

        private const float CoreHaloSize = 4.6f;
        private const float CoreSize = 2.2f;
        private const float CoreGrowScale = 1.75f;
        private const float CorePulseSpeed = 3.2f;
        private const float CorePulseAmount = 0.12f;

        private const int StreamPerTick = 2;
        private const float StreamMinSpeed = 8f;
        private const float StreamTravelSecs = 1.8f;
        private const float StreamJitter = 0.18f;
        private const float StreamFadeInSecs = 0.4f;
        private const float FirstQuoteHeight = 2.7f;
        private const float SecondQuoteHeight = 2.1f;
        private const float FirstQuoteSecs = 1.5f;
        private const float SecondQuoteSecs = 2.6f;
        private const float AsphodelCoreLift = 0.28f;

        private static readonly FloatRange AshScale = new FloatRange(1f, 2f);
        private static readonly FloatRange AshSpeed = new FloatRange(0.5f, 1.3f);
        private static readonly FloatRange EmberScale = new FloatRange(0.3f, 0.7f);
        private static readonly FloatRange EmberSpeed = new FloatRange(1f, 2.4f);
        private static readonly FloatRange StreamScale = new FloatRange(0.5f, 0.85f);
        private static readonly Color AshColor = new Color(0.12f, 0.1f, 0.13f, 0.9f);
        private static readonly Color EmberColor = new Color(0.78f, 0.36f, 1f, 1f);
        private static readonly Color GlintColor = new Color(1f, 0.95f, 0.82f, 1f);
        private static readonly Color StreamColor = new Color(1f, 1f, 0.97f, 1f);

        private static readonly DissolveStyle BurnStyle = new DissolveStyle(BurnDelaySecs, BurnDurationSecs, new Color(0.72f, 0.3f, 1f, 1f), 0f, 1f);
        private static readonly Material CoreHaloMat = MaterialPool.MatFrom("VFX/VV_ScentAura", ShaderDatabase.MoteGlow, new Color(1f, 0.9f, 0.72f, 0.9f));
        private static readonly Material CoreMat = MaterialPool.MatFrom("Things/Mote/SparkFlash", ShaderDatabase.MoteGlow, new Color(1f, 0.98f, 0.92f, 1f));

        private ThingOwner<Pawn> _innerContainer;
        private string _texPath;
        private string _morphTexPath;
        private float _morphDrawSize = 3f;
        private Rot4 _rotation = Rot4.South;
        private float _drawSize = 8.5f;
        private Vector3 _drawOffset;
        private Vector3 _bodyOffset;
        private int _startTick = -1;

        public Pawn Boss => _innerContainer.Count > 0 ? _innerContainer[0] : null;

        public int Age => _startTick < 0 ? 0 : Mathf.Max(0, Find.TickManager.TicksGame - _startTick);

        public int DurationTicks => Boss?.TryGetComp<CompApocrypha>()?.Props.rebirthTicks ?? 540;

        public int TicksLeft => Mathf.Max(0, DurationTicks - Age);

        private Vector3 QueenCorePos => DrawPos + _drawOffset + _bodyOffset;

        private Vector3 AsphodelCorePos => Position.ToVector3Shifted() + new Vector3(0f, 0f, AsphodelCoreLift);

        private float GrowProgress => Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(GrowStartTick, MorphStartTick, Age));

        private float MorphProgress => Mathf.InverseLerp(MorphStartTick, DurationTicks, Age);

        public Vector3 CorePos => Vector3.Lerp(QueenCorePos, AsphodelCorePos, GrowProgress);

        public ApocryphaRebirth()
        {
            _innerContainer = new ThingOwner<Pawn>(this, oneStackOnly: true, LookMode.Deep);
        }

        public void Init(Pawn boss)
        {
            var graphicData = boss.ageTracker?.CurKindLifeStage?.bodyGraphicData;
            _texPath = graphicData?.texPath;
            _drawSize = graphicData?.drawSize.x ?? 8.5f;
            _rotation = boss.Rotation;
            _drawOffset = EndingVfxUtility.OffsetFromCell(boss.DrawPos, boss.Position);
            _bodyOffset = ApocryphaGimmicks.GetBossBodyOffset(boss);

            var stages = boss.kindDef.lifeStages;
            var morphData = stages[stages.Count - 1].bodyGraphicData;
            _morphTexPath = morphData?.texPath;
            _morphDrawSize = morphData?.drawSize.x ?? 3f;
        }

        public void Hold(Pawn boss)
        {
            boss.DeSpawnOrDeselect();
            _innerContainer.TryAdd(boss);
        }

        public override void SpawnSetup(Map map, bool respawningAfterLoad)
        {
            base.SpawnSetup(map, respawningAfterLoad);

            if (!respawningAfterLoad && _startTick < 0)
            {
                _startTick = Find.TickManager.TicksGame;
            }
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Deep.Look(ref _innerContainer, "innerContainer", this);
            Scribe_Values.Look(ref _texPath, "texPath");
            Scribe_Values.Look(ref _morphTexPath, "morphTexPath");
            Scribe_Values.Look(ref _morphDrawSize, "morphDrawSize", 3f);
            Scribe_Values.Look(ref _rotation, "rotation", Rot4.South);
            Scribe_Values.Look(ref _drawSize, "drawSize", 8.5f);
            Scribe_Values.Look(ref _drawOffset, "drawOffset", Vector3.zero);
            Scribe_Values.Look(ref _bodyOffset, "bodyOffset", Vector3.zero);
            Scribe_Values.Look(ref _startTick, "startTick", -1);
        }

        public ThingOwner GetDirectlyHeldThings()
        {
            return _innerContainer;
        }

        public void GetChildHolders(List<IThingHolder> outChildren)
        {
            ThingOwnerUtility.AppendThingHoldersFromThings(outChildren, GetDirectlyHeldThings());
        }

        protected override void Tick()
        {
            base.Tick();
            if (!Spawned) { return; }

            var age = Age;
            if (age >= DurationTicks)
            {
                Complete();
                return;
            }

            var map = Map;
            var core = CorePos;

            if (age == FirstQuoteTick || age == SecondQuoteTick)
            {
                var first = age == FirstQuoteTick;
                var key = first ? LocalizeString_Etc.VV_Speech_AsphodelRebirth1 : LocalizeString_Etc.VV_Speech_AsphodelRebirth2;
                MoteMaker.ThrowText(core + new Vector3(0f, 0f, first ? FirstQuoteHeight : SecondQuoteHeight), map, key.Translate(), first ? FirstQuoteSecs : SecondQuoteSecs);
            }

            if (!core.ShouldSpawnMotesAt(map)) { return; }

            if (age < BurnTicks)
            {
                var center = DrawPos + _drawOffset;
                var progress = Mathf.Clamp01((age / 60f - BurnDelaySecs) / BurnDurationSecs);
                if (progress > 0f && progress < 1f)
                {
                    DissolveRenderer.ThrowAshAndEmbers(
                        map, center, center.z + _drawSize * (0.42f - 0.8f * progress), _drawSize * 0.32f, age, AshInterval, EmberInterval,
                        ashJitterZ: 0.3f, AshScale, AshSpeed,
                        emberJitterZ: 0.2f, EmberScale, EmberSpeed,
                        AshColor, VVFleckDefOf.VV_Fleck_ApocryphaManifestSpark, EmberColor);
                }
            }

            if (age >= GrowStartTick && age < MorphStartTick)
            {
                ThrowFairyStream(map, core, age);
            }

            if (age % GlintInterval == 0)
            {
                var data = FleckMaker.GetDataStatic(core + Gen.RandomHorizontalVector(0.6f), map, VVFleckDefOf.VV_Fleck_ApocryphaEmber, Rand.Range(0.25f, 0.5f));
                data.velocityAngle = Rand.Range(0f, 360f);
                data.velocitySpeed = Rand.Range(0.3f, 0.9f);
                data.instanceColor = GlintColor;
                map.flecks.CreateFleck(data);
            }
        }

        private void ThrowFairyStream(Map map, Vector3 core, int age)
        {
            if (!ApocryphaGimmicks.TryGetEverflowerBudPos(map, out var bud)) { return; }

            var rate = StreamPerTick * Mathf.Clamp01((age - GrowStartTick) / (StreamFadeInSecs * 60f));
            var side = EndingVfxUtility.HorizontalPerpendicular(EndingVfxUtility.SafeNormalizeHorizontal(core - bud, Vector3.forward));
            var speed = Mathf.Max(StreamMinSpeed, (core - bud).MagnitudeHorizontal() / StreamTravelSecs);
            for (int i = GenMath.RoundRandom(rate); i > 0; i--)
            {
                var from = bud + side * Rand.Range(-StreamJitter, StreamJitter);
                ApocryphaGimmicks.ThrowFairyLight(map, from, core.Yto0(), speed * Rand.Range(0.9f, 1.1f), StreamScale.RandomInRange, StreamColor);
            }
        }

        private void Complete()
        {
            var map = Map;
            var boss = Boss;
            if (boss != null)
            {
                var cell = Position;
                if (!cell.Standable(map))
                {
                    var near = CellFinder.StandableCellNear(cell, map, 6f);
                    if (near.IsValid)
                    {
                        cell = near;
                    }
                }

                _innerContainer.Remove(boss);
                GenSpawn.Spawn(boss, cell, map, Rot4.South);
                boss.TryGetComp<CompApocrypha>()?.Notify_Reborn();
            }

            Destroy();
        }

        public override void Destroy(DestroyMode mode = DestroyMode.Vanish)
        {
            _innerContainer.ClearAndDestroyContents();
            base.Destroy(mode);
        }

        protected override void DrawAt(Vector3 drawLoc, bool flip = false)
        {
            var age = Age;
            var secs = age / 60f;

            if (!_texPath.NullOrEmpty() && secs < BurnDelaySecs + BurnDurationSecs + 0.2f)
            {
                var pos = drawLoc + _drawOffset;
                pos.y = AltitudeLayer.Pawn.AltitudeFor();
                DissolveRenderer.Draw(DissolveRenderer.GetMaterial(_texPath, _rotation), pos, new Vector2(_drawSize, _drawSize), _rotation == Rot4.West, secs, BurnStyle, Vector2.right);
            }

            var morph = MorphProgress;
            if (morph > 0f && !_morphTexPath.NullOrEmpty())
            {
                var bodyPos = Position.ToVector3Shifted();
                bodyPos.y = AltitudeLayer.Pawn.AltitudeFor();
                ManifestationRenderer.Draw(_morphTexPath, Rot4.South, bodyPos, _morphDrawSize, ageSecs: (age - MorphStartTick) / 60f,
                    progress: 1f - Mathf.Pow(1f - morph, 2.2f), reveal: Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.5f, 1f, morph)));
            }

            var burn = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(secs / (BurnTicks / 60f)));
            var scale = Mathf.Lerp(1f, CoreGrowScale, GrowProgress) * (1f - Mathf.SmoothStep(0f, 1f, morph));
            var pulse = 1f + CorePulseAmount * Mathf.Sin(secs * CorePulseSpeed * Mathf.PI);
            var core = CorePos;
            core.y = AltitudeLayer.MoteOverhead.AltitudeFor();

            DrawQuad(CoreHaloMat, core, CoreHaloSize * Mathf.Lerp(0.4f, 1f, burn) * scale * pulse);
            core.y += 0.01f;
            DrawQuad(CoreMat, core, CoreSize * Mathf.Lerp(0.6f, 1f, burn) * scale * pulse);
        }

        private static void DrawQuad(Material material, Vector3 pos, float size)
        {
            if (size <= 0.01f) { return; }

            var matrix = Matrix4x4.TRS(pos, Quaternion.identity, new Vector3(size, 1f, size));
            Graphics.DrawMesh(MeshPool.plane10, matrix, material, 0);
        }

        public override string GetInspectString()
        {
            return LocalizeString_Inspector.VV_Inspector_ApocryphaPhase.Translate(CompApocrypha.PhaseLabel(ApocryphaPhase.Rebirth));
        }

        public override IEnumerable<Gizmo> GetGizmos()
        {
            foreach (var gizmo in base.GetGizmos())
            {
                yield return gizmo;
            }

            if (DebugSettings.godMode)
            {
                yield return EndingVfxUtility.DevAction("DEV: Finish rebirth now", Complete);
            }
        }
    }
}
