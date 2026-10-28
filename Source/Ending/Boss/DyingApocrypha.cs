using RimWorld;
using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace VVRace
{
    // 처치된 아포크리파의 소멸 연출(DyingRevenant 패턴). 저장되므로 연출 도중 저장/불러오기를 해도 이어진다.
    // 마지막 방향·크기·2페이즈 그래픽을 기억해 VV_Dissolve 셰이더로 위에서부터 황금 테두리를 남기며 녹아내리는 모습을 그리고,
    // 녹는 경계에서 재와 금빛 불씨를 피워 올린다. 끝나면 엔딩 컨트롤러에 토벌을 알리고 사라진다.
    [StaticConstructorOnStartup]
    public class DyingApocrypha : Thing
    {
        public const int DissolveTicks = 450;

        private const float DissolveDelaySecs = 0.6f;
        private const float DissolveDurationSecs = 6.4f;
        private const int AshInterval = 2;
        private const int EmberInterval = 3;
        private const int ShakeInterval = 60;

        private static readonly Color EdgeColor = new Color(1f, 0.78f, 0.35f, 1f);
        private static readonly MaterialPropertyBlock PropertyBlock = new MaterialPropertyBlock();
        private static readonly Dictionary<string, Material> MaterialCache = new Dictionary<string, Material>();

        private string _texPath;
        private Rot4 _rotation = Rot4.South;
        private float _drawSize = 8.5f;
        private Vector3 _drawOffset;
        private int _startTick = -1;
        private bool _notified;

        private float AgeSecs => Mathf.Max(0, Find.TickManager.TicksGame - _startTick) / 60f;

        // 녹아내리는 정도(0~1). 노이즈가 위쪽부터 낮으므로 경계는 위에서 아래로 내려온다.
        private float DissolveProgress => Mathf.Clamp01((AgeSecs - DissolveDelaySecs) / DissolveDurationSecs);

        public void InitFrom(Pawn boss)
        {
            var comp = boss.TryGetComp<CompApocrypha>();
            var graphicData = boss.ageTracker?.CurKindLifeStage?.bodyGraphicData;

            _texPath = graphicData?.texPath;
            _rotation = boss.Rotation;
            _drawSize = comp?.CurrentVisualDrawSize ?? graphicData?.drawSize.x ?? 8.5f;

            var offset = boss.DrawPos - boss.Position.ToVector3Shifted();
            _drawOffset = new Vector3(offset.x, 0f, offset.z);
        }

        public override void SpawnSetup(Map map, bool respawningAfterLoad)
        {
            base.SpawnSetup(map, respawningAfterLoad);

            if (!respawningAfterLoad && _startTick < 0)
            {
                _startTick = Find.TickManager.TicksGame;

                ApocryphaEffecterDefOf.VV_Effecter_ApocryphaDeath.Spawn(Position, map, _drawOffset).Cleanup();
                ApocryphaUtility.ShakeCamera(map, 2f);
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
        }

        protected override void Tick()
        {
            base.Tick();
            if (!Spawned) { return; }

            var age = Find.TickManager.TicksGame - _startTick;
            if (age >= DissolveTicks)
            {
                Complete();
                return;
            }

            ThrowDissolveParticles(age);
        }

        // 녹아내리는 경계 높이에서 재(먼지)와 금빛 불씨가 피어오른다.
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

            if (age % AshInterval == 0)
            {
                var pos = new Vector3(center.x + Rand.Range(-halfWidth, halfWidth), center.y, frontZ + Rand.Range(-0.3f, 0.3f));
                var data = FleckMaker.GetDataStatic(pos, Map, ApocryphaFleckDefOf.VV_Fleck_ApocryphaAsh, Rand.Range(1f, 1.9f));
                data.velocityAngle = Rand.Range(-35f, 35f);
                data.velocitySpeed = Rand.Range(0.4f, 1.1f);
                data.rotation = Rand.Range(0f, 360f);
                data.rotationRate = Rand.Range(-50f, 50f);
                Map.flecks.CreateFleck(data);
            }

            if (age % EmberInterval == 0)
            {
                var pos = new Vector3(center.x + Rand.Range(-halfWidth, halfWidth), center.y, frontZ + Rand.Range(-0.2f, 0.2f));
                var data = FleckMaker.GetDataStatic(pos, Map, ApocryphaFleckDefOf.VV_Fleck_ApocryphaEmber, Rand.Range(0.25f, 0.6f));
                data.velocityAngle = Rand.Range(-25f, 25f);
                data.velocitySpeed = Rand.Range(0.8f, 2f);
                data.rotation = Rand.Range(0f, 360f);
                Map.flecks.CreateFleck(data);
            }
        }

        private void Complete()
        {
            var map = Map;
            FilthMaker.TryMakeFilth(Position, map, ThingDefOf.Filth_Ash, 3);
            ApocryphaEffecterDefOf.VV_Effecter_ApocryphaVanish.Spawn(Position, map, 2f).Cleanup();

            if (!_notified)
            {
                _notified = true;
                ViviEndingQuest.Current?.Notify_BossDefeated(null);
            }

            if (!Destroyed)
            {
                Destroy();
            }
        }

        protected override void DrawAt(Vector3 drawLoc, bool flip = false)
        {
            var material = GetMaterial(_texPath, _rotation);
            if (material == null) { return; }

            var pos = drawLoc + _drawOffset;
            pos.y = AltitudeLayer.Pawn.AltitudeFor();

            PropertyBlock.Clear();
            PropertyBlock.SetFloat(ShaderPropertyIDs.AgeSecs, AgeSecs);

            // 서쪽은 동쪽 텍스처를 좌우 반전해 그린다(Graphic_Multi와 같은 규칙).
            var mesh = _rotation == Rot4.West ? MeshPool.plane10Flip : MeshPool.plane10;
            var matrix = Matrix4x4.TRS(pos, Quaternion.identity, new Vector3(_drawSize, 1f, _drawSize));
            Graphics.DrawMesh(mesh, matrix, material, 0, null, 0, PropertyBlock);
        }

        private static Material GetMaterial(string texPath, Rot4 rotation)
        {
            if (texPath.NullOrEmpty()) { return null; }

            var suffix = rotation == Rot4.North ? "_north" : (rotation == Rot4.South ? "_south" : "_east");
            var fullPath = texPath + suffix;
            if (MaterialCache.TryGetValue(fullPath, out var cached))
            {
                return cached;
            }

            var texture = ContentFinder<Texture2D>.Get(fullPath, reportFailure: false);
            var noise = ContentFinder<Texture2D>.Get("VFX/VV_DissolveNoise", reportFailure: false);
            Material material = null;
            if (texture != null)
            {
                // (텍스처, VV_Dissolve, 흰색) 요청은 이 연출만 쓰므로 공유 재질의 매개변수를 직접 바꿔도 다른 곳에 영향이 없다.
                material = MaterialPool.MatFrom(texture, ShaderDatabase.LoadShader("Map/VV_Dissolve"), Color.white);
                if (noise != null)
                {
                    material.SetTexture("_DissolveTex", noise);
                }
                material.SetColor("_EdgeColor", EdgeColor);
                material.SetFloat("_DissolveDelay", DissolveDelaySecs);
                material.SetFloat("_DissolveDuration", DissolveDurationSecs);
            }

            MaterialCache[fullPath] = material;
            return material;
        }
    }
}
