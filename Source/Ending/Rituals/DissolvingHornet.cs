using RimWorld;
using UnityEngine;
using Verse;

namespace VVRace
{
    [StaticConstructorOnStartup]
    public class DissolvingHornet : Thing
    {
        private const float DissolveDelaySecs = 0.15f;
        private const float DissolveDurationSecs = 2.2f;
        private const int LifeTicks = 150;
        private const int DustInterval = 2;
        private const int PetalInterval = 5;

        private static readonly DissolveStyle Style = new DissolveStyle(DissolveDelaySecs, DissolveDurationSecs, new Color(1f, 0.86f, 0.5f, 1f), 0.85f, 0.45f);
        private static readonly Color[] DustColors =
        {
            new Color(0.62f, 0.58f, 0.52f, 0.8f),
            new Color(0.78f, 0.72f, 0.6f, 0.75f),
            new Color(1f, 0.9f, 0.66f, 0.7f),
        };

        private string _texPath;
        private Rot4 _rotation = Rot4.South;
        private float _drawSize = 2.5f;
        private Vector3 _drawOffset;
        private Vector3 _wind = Vector3.forward;
        private int _startTick = -1;

        private float AgeSecs => Mathf.Max(0, Find.TickManager.TicksGame - _startTick) / 60f;

        private float Progress => Mathf.Clamp01((AgeSecs - DissolveDelaySecs) / DissolveDurationSecs);

        public void InitFrom(Pawn hornet, Vector3 wind)
        {
            var graphicData = hornet.ageTracker?.CurKindLifeStage?.bodyGraphicData;
            _texPath = graphicData?.texPath;
            _drawSize = graphicData?.drawSize.x ?? 2.5f;
            _rotation = hornet.Rotation;

            var offset = hornet.DrawPos - hornet.Position.ToVector3Shifted();
            _drawOffset = new Vector3(offset.x, 0f, offset.z);

            wind.y = 0f;
            _wind = wind.sqrMagnitude > 0.0001f ? wind.normalized : Vector3.forward;
        }

        public override void SpawnSetup(Map map, bool respawningAfterLoad)
        {
            base.SpawnSetup(map, respawningAfterLoad);

            if (!respawningAfterLoad && _startTick < 0)
            {
                _startTick = Find.TickManager.TicksGame;
                QueenDefOf.VV_Effecter_HornetDissolve?.Spawn(Position, map, _drawOffset).Cleanup();
            }
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref _texPath, "texPath");
            Scribe_Values.Look(ref _rotation, "rotation", Rot4.South);
            Scribe_Values.Look(ref _drawSize, "drawSize", 2.5f);
            Scribe_Values.Look(ref _drawOffset, "drawOffset", Vector3.zero);
            Scribe_Values.Look(ref _wind, "wind", Vector3.forward);
            Scribe_Values.Look(ref _startTick, "startTick", -1);
        }

        protected override void Tick()
        {
            base.Tick();
            if (!Spawned) { return; }

            var age = Find.TickManager.TicksGame - _startTick;
            if (age >= LifeTicks)
            {
                Destroy();
                return;
            }

            ThrowDust(age);
        }

        // 셰이더가 바람이 부는 쪽 가장자리부터 지우므로 가루가 날리는 경계도 같은 축을 따라 바람 반대쪽으로 이동한다.
        private void ThrowDust(int age)
        {
            var progress = Progress;
            if (progress <= 0f || progress >= 1f) { return; }

            var map = Map;
            var center = DrawPos + _drawOffset;
            if (!center.ShouldSpawnMotesAt(map)) { return; }

            var front = _drawSize * 0.8f * (0.5f - progress);
            var side = new Vector3(_wind.z, 0f, -_wind.x);
            var halfWidth = _drawSize * 0.3f;
            var windAngle = _wind.AngleFlat();

            if (age % DustInterval == 0 && QueenDefOf.VV_Fleck_HornetDust != null)
            {
                for (int i = 0; i < 2; i++)
                {
                    var pos = center + _wind * (front + Rand.Range(-0.15f, 0.15f)) + side * Rand.Range(-halfWidth, halfWidth);
                    var data = FleckMaker.GetDataStatic(pos, map, QueenDefOf.VV_Fleck_HornetDust, Rand.Range(0.5f, 1.1f));
                    data.velocityAngle = windAngle + Rand.Range(-30f, 30f);
                    data.velocitySpeed = Rand.Range(1.2f, 2.8f);
                    data.rotation = Rand.Range(0f, 360f);
                    data.rotationRate = Rand.Range(-90f, 90f);
                    data.instanceColor = DustColors.RandomElement();
                    map.flecks.CreateFleck(data);
                }
            }

            var petalDef = EndingCoreDefOf.VV_Fleck_EndingPetal;
            if (age % PetalInterval == 0 && petalDef != null)
            {
                var pos = center + _wind * front + side * Rand.Range(-halfWidth, halfWidth);
                var petal = FleckMaker.GetDataStatic(pos, map, petalDef, Rand.Range(0.35f, 0.6f));
                petal.velocityAngle = windAngle + Rand.Range(-20f, 20f);
                petal.velocitySpeed = Rand.Range(1.5f, 3f);
                petal.rotation = Rand.Range(0f, 360f);
                petal.rotationRate = Rand.Range(-240f, 240f);
                map.flecks.CreateFleck(petal);
            }
        }

        protected override void DrawAt(Vector3 drawLoc, bool flip = false)
        {
            if (_texPath.NullOrEmpty()) { return; }

            var pos = drawLoc + _drawOffset;
            pos.y = AltitudeLayer.Pawn.AltitudeFor();

            DissolveRenderer.Draw(
                DissolveRenderer.GetMaterial(_texPath, _rotation),
                pos,
                new Vector2(_drawSize, _drawSize),
                _rotation == Rot4.West,
                AgeSecs,
                Style,
                new Vector2(_wind.x, _wind.z));
        }
    }
}
