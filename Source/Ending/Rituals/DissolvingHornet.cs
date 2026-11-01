using RimWorld;
using System.Collections.Generic;
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
        private const float SweepWeight = 0.85f;
        private const float AshAmount = 0.45f;

        private static readonly Color EdgeColor = new Color(1f, 0.86f, 0.5f, 1f);
        private static readonly Color[] DustColors =
        {
            new Color(0.62f, 0.58f, 0.52f, 0.8f),
            new Color(0.78f, 0.72f, 0.6f, 0.75f),
            new Color(1f, 0.9f, 0.66f, 0.7f),
        };

        private static readonly int SweepDirId = Shader.PropertyToID("_SweepDir");
        private static readonly int SweepWeightId = Shader.PropertyToID("_SweepWeight");
        private static readonly int AshAmountId = Shader.PropertyToID("_AshAmount");
        private static readonly int EdgeColorId = Shader.PropertyToID("_EdgeColor");
        private static readonly int DissolveDelayId = Shader.PropertyToID("_DissolveDelay");
        private static readonly int DissolveDurationId = Shader.PropertyToID("_DissolveDuration");
        private static readonly MaterialPropertyBlock PropertyBlock = new MaterialPropertyBlock();
        private static readonly Dictionary<string, Material> MaterialCache = new Dictionary<string, Material>();

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

            var suffix = _rotation == Rot4.North ? "_north" : (_rotation == Rot4.South ? "_south" : "_east");
            var material = GetMaterial(_texPath + suffix);
            if (material == null) { return; }

            var pos = drawLoc + _drawOffset;
            pos.y = AltitudeLayer.Pawn.AltitudeFor();

            // Graphic_Multi처럼 서쪽은 동쪽 텍스처를 좌우 반전한다. 메시의 UV도 반전되므로 쓸기 방향의 x도 뒤집는다.
            var westFlip = _rotation == Rot4.West;

            PropertyBlock.Clear();
            PropertyBlock.SetFloat(ShaderPropertyIDs.AgeSecs, AgeSecs);
            PropertyBlock.SetFloat(DissolveDelayId, DissolveDelaySecs);
            PropertyBlock.SetFloat(DissolveDurationId, DissolveDurationSecs);
            PropertyBlock.SetColor(EdgeColorId, EdgeColor);
            PropertyBlock.SetVector(SweepDirId, new Vector4(westFlip ? -_wind.x : _wind.x, _wind.z, 0f, 0f));
            PropertyBlock.SetFloat(SweepWeightId, SweepWeight);
            PropertyBlock.SetFloat(AshAmountId, AshAmount);

            var mesh = westFlip ? MeshPool.plane10Flip : MeshPool.plane10;
            var matrix = Matrix4x4.TRS(pos, Quaternion.identity, new Vector3(_drawSize, 1f, _drawSize));
            Graphics.DrawMesh(mesh, matrix, material, 0, null, 0, PropertyBlock);
        }

        private static Material GetMaterial(string fullPath)
        {
            if (MaterialCache.TryGetValue(fullPath, out var cached))
            {
                return cached;
            }

            var texture = ContentFinder<Texture2D>.Get(fullPath, reportFailure: false);
            var noise = ContentFinder<Texture2D>.Get("VFX/VV_DissolveNoise", reportFailure: false);
            Material material = null;
            if (texture != null)
            {
                // 연출 값은 그릴 때 MaterialPropertyBlock으로 넣으므로 공유 재질에는 노이즈 텍스처만 지정한다.
                material = MaterialPool.MatFrom(texture, ShaderDatabase.LoadShader("Map/VV_Dissolve"), Color.white);
                if (noise != null)
                {
                    material.SetTexture("_DissolveTex", noise);
                }
            }

            MaterialCache[fullPath] = material;
            return material;
        }
    }
}
