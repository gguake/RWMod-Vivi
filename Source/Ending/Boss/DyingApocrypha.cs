using RimWorld;
using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace VVRace
{
    // 처치된 아포크리파의 소멸 연출(DyingRevenant 패턴). 저장되므로 연출 도중 저장/불러오기를 해도 이어진다.
    // 마지막 방향·크기·2페이즈 그래픽을 기억해 VV_Dissolve 셰이더로 위에서부터 황금 테두리를 남기며 녹아내리는 모습을 그리고,
    // 녹는 경계에서 재와 금빛 불씨를 피워 올린다. 끝나면 (엔딩 퀘스트의 보스였다면) 엔딩 컨트롤러에 토벌을 알리고 사라진다.
    // 찬탈 모드(InitStolen): 영원꽃을 빼앗고 떠나는 모습. 영원꽃이 꽃잎으로 흩어져 보스에게 빨려 들고,
    // 이어 보스도 바람이 부는 쪽 가장자리부터 꽃잎으로 흩날리며 떠오르듯 사라진다. 토벌이 아니므로 퀘스트에 알리지 않는다.
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

        // 찬탈 모드: 영원꽃이 먼저 흩어지고, 꽃잎이 보스에게 닿을 무렵 보스가 흩날리기 시작한다.
        private const float FlowerDelaySecs = 0.15f;
        private const float FlowerDurationSecs = 2.2f;
        private const float StolenBossDelaySecs = 1.3f;
        private const float StolenBossDurationSecs = 3.8f;
        private const float StolenSweepWeight = 0.85f;
        private const float StolenRiseCells = 0.9f;

        private static readonly Color EdgeColor = new Color(1f, 0.78f, 0.35f, 1f);
        private static readonly Color FlowerEdgeColor = new Color(1f, 0.93f, 0.72f, 1f);
        private static readonly Color StolenEdgeColor = new Color(1f, 0.62f, 0.88f, 1f);
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
        private float _drawSize = 8.5f;
        private Vector3 _drawOffset;
        private int _startTick = -1;
        private bool _notified;

        // 엔딩 퀘스트의 보스였는지. 퀘스트와 무관한 아포크리파(개발자 도구로 소환 등)의 소멸은 토벌로 알리지 않는다.
        private bool _questBoss = true;

        // 찬탈 모드: 영원꽃 그림(Graphic_Single 경로·그리는 중심·크기), 꽃봉오리 위치, 보스 몸통 중심 오프셋, 꽃잎이 날아가는 방향(평면 단위 벡터)
        private bool _stolen;
        private string _flowerTexPath;
        private Vector3 _flowerDrawPos;
        private Vector2 _flowerDrawSize;
        private Vector3 _flowerBudPos;
        private Vector3 _bodyOffset;
        private Vector3 _wind = Vector3.forward;

        private float AgeSecs => Mathf.Max(0, Find.TickManager.TicksGame - _startTick) / 60f;

        // 녹아내리는 정도(0~1). 노이즈가 위쪽부터 낮으므로 경계는 위에서 아래로 내려온다.
        private float DissolveProgress => Mathf.Clamp01((AgeSecs - DissolveDelaySecs) / DissolveDurationSecs);

        private float FlowerProgress => Mathf.Clamp01((AgeSecs - FlowerDelaySecs) / FlowerDurationSecs);
        private float StolenBossProgress => Mathf.Clamp01((AgeSecs - StolenBossDelaySecs) / StolenBossDurationSecs);

        // 찬탈 모드에서 흩날리는 동안 보스가 떠오르며 바람 쪽으로 살짝 밀려나는 거리
        private Vector3 StolenDrift
        {
            get
            {
                var p = StolenBossProgress;
                p *= p;
                return new Vector3(_wind.x * 0.4f * p, 0f, StolenRiseCells * p);
            }
        }

        public void InitFrom(Pawn boss)
        {
            var comp = boss.TryGetComp<CompApocrypha>();
            var graphicData = boss.ageTracker?.CurKindLifeStage?.bodyGraphicData;

            _texPath = graphicData?.texPath;
            _rotation = boss.Rotation;
            _drawSize = comp?.CurrentVisualDrawSize ?? graphicData?.drawSize.x ?? 8.5f;
            _questBoss = comp != null && comp.IsQuestBossFight;

            var offset = boss.DrawPos - boss.Position.ToVector3Shifted();
            _drawOffset = new Vector3(offset.x, 0f, offset.z);
        }

        // 찬탈 모드로 초기화한다. 영원꽃과 보스를 없애기 전에 불러 지금 모습을 기억한다.
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

            // 꽃잎은 영원꽃에서 보스 쪽으로, 조금 위(화면 위쪽)로 치우쳐 날아간다.
            var away = bossCenter - budPos;
            away.y = 0f;
            away = away.sqrMagnitude > 0.25f ? away.normalized : Vector3.right;
            _wind = (away * 0.75f + Vector3.forward * 0.55f).normalized;
        }

        public override void SpawnSetup(Map map, bool respawningAfterLoad)
        {
            base.SpawnSetup(map, respawningAfterLoad);

            if (!respawningAfterLoad && _startTick < 0)
            {
                _startTick = Find.TickManager.TicksGame;

                // 찬탈 모드는 찬탈 순간의 연출(CompApocrypha.StealEverflower)이 이미 터졌다.
                if (!_stolen)
                {
                    ApocryphaEffecterDefOf.VV_Effecter_ApocryphaDeath.Spawn(Position, map, _drawOffset).Cleanup();
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

            // 기본값을 true로 두어 이 값이 없던 저장(소멸 도중 저장)도 예전처럼 퀘스트에 알린다.
            Scribe_Values.Look(ref _questBoss, "questBoss", true);

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

        // 찬탈 모드: 영원꽃의 꽃잎은 보스에게 빨려 들고, 보스의 꽃잎은 흩날리는 경계에서 바람을 타고 날아간다.
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

            // 셰이더는 바람 쪽 가장자리부터 지우므로, 경계는 바람 축을 따라 바람 쪽 끝에서 반대쪽 끝으로 옮겨 간다.
            var center = DrawPos + _drawOffset + StolenDrift;
            var front = _drawSize * 0.8f * (0.5f - bossProgress);
            var side = new Vector3(_wind.z, 0f, -_wind.x);
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
            var data = FleckMaker.GetDataStatic(pos, Map, ApocryphaFleckDefOf.VV_Fleck_ApocryphaStealPetal, scale);
            data.velocityAngle = angle;
            data.velocitySpeed = speed;
            data.rotation = Rand.Range(0f, 360f);
            data.rotationRate = Rand.Range(-240f, 240f);
            data.instanceColor = colors.RandomElement();
            Map.flecks.CreateFleck(data);
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
            if (!_stolen)
            {
                FilthMaker.TryMakeFilth(Position, map, ThingDefOf.Filth_Ash, 3);
                ApocryphaEffecterDefOf.VV_Effecter_ApocryphaVanish.Spawn(Position, map, 2f).Cleanup();
            }

            NotifyDefeated();

            if (!Destroyed)
            {
                Destroy();
            }
        }

        // 연출이 끝나기 전에 사라져도(산 지붕 붕괴에 깔림 등) 토벌 통지가 빠지지 않게 한다.
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

            var suffix = _rotation == Rot4.North ? "_north" : (_rotation == Rot4.South ? "_south" : "_east");
            var material = GetMaterial(_texPath + suffix);
            if (material == null) { return; }

            var pos = drawLoc + _drawOffset;
            pos.y = AltitudeLayer.Pawn.AltitudeFor();

            PropertyBlock.Clear();
            PropertyBlock.SetFloat(ShaderPropertyIDs.AgeSecs, AgeSecs);

            // 서쪽은 동쪽 텍스처를 좌우 반전해 그린다(Graphic_Multi와 같은 규칙). 반전 메시는 UV도 뒤집히므로 지우는 방향의 x도 뒤집는다.
            var westFlip = _rotation == Rot4.West;
            if (_stolen)
            {
                pos += StolenDrift;
                SetStolenParams(StolenBossDelaySecs, StolenBossDurationSecs, StolenEdgeColor, westFlip ? -_wind.x : _wind.x);
            }

            var mesh = westFlip ? MeshPool.plane10Flip : MeshPool.plane10;
            var matrix = Matrix4x4.TRS(pos, Quaternion.identity, new Vector3(_drawSize, 1f, _drawSize));
            Graphics.DrawMesh(mesh, matrix, material, 0, null, 0, PropertyBlock);
        }

        // 찬탈 모드: 사라진 영원꽃 자리에 같은 그림을 그려, 바람 쪽 가장자리부터 꽃잎으로 흩어지게 한다.
        private void DrawStolenFlower()
        {
            if (_flowerTexPath.NullOrEmpty() || FlowerProgress >= 1f) { return; }

            var material = GetMaterial(_flowerTexPath);
            if (material == null) { return; }

            PropertyBlock.Clear();
            PropertyBlock.SetFloat(ShaderPropertyIDs.AgeSecs, AgeSecs);
            SetStolenParams(FlowerDelaySecs, FlowerDurationSecs, FlowerEdgeColor, _wind.x);

            var matrix = Matrix4x4.TRS(_flowerDrawPos, Quaternion.identity, new Vector3(_flowerDrawSize.x, 1f, _flowerDrawSize.y));
            Graphics.DrawMesh(MeshPool.plane10, matrix, material, 0, null, 0, PropertyBlock);
        }

        // 공유 재질(죽음 연출 값)은 그대로 두고 이번 그리기에만 찬탈 연출 값을 덮어쓴다.
        private void SetStolenParams(float delay, float duration, Color edgeColor, float sweepX)
        {
            PropertyBlock.SetFloat(DissolveDelayId, delay);
            PropertyBlock.SetFloat(DissolveDurationId, duration);
            PropertyBlock.SetColor(EdgeColorId, edgeColor);
            PropertyBlock.SetVector(SweepDirId, new Vector4(sweepX, _wind.z, 0f, 0f));
            PropertyBlock.SetFloat(SweepWeightId, StolenSweepWeight);
            PropertyBlock.SetFloat(AshAmountId, 0f);
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
