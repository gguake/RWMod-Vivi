using RimWorld;
using System.Collections.Generic;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace VVRace
{
    // 아포크리파 1페이즈 등장 연출(저장 가능한 시퀀서). 보스 폰은 연출이 끝날 때 퀘스트(QuestPart_ViviApocrypha)가 따로 스폰한다.
    // 1) 모임(0~5초): 영원꽃 꽃봉오리 위 허공으로 검은 에너지 입자가 모여들며 VV_Manifest 셰이더로 여왕의 형체가 몸 중심부터 빚어진다.
    // 2) 완성(5~5.7초): 형체가 다 빚어져 어둡게 본래 색이 배어 나오고, 영원꽃 꽃봉오리에 금빛이 모인다.
    // 3) 파장(5.7초~): 영원꽃에서 맵 전체로 금빛 파장이 퍼지고, 형체가 그 파장에 밀려 실제 등장 칸(맵 외곽)까지 날아가 내려앉는다.
    //    내려앉는 틱(SpawnTick)에 퀘스트가 실제 보스를 같은 자리·방향·크기로 스폰하고 이 연출을 지운다.
    [StaticConstructorOnStartup]
    public class ApocryphaManifestation : Thing
    {
        // 단계 시점(연출 시작부터의 틱)
        private const int CompleteTick = 300;
        private const int WaveTick = 342;

        // 형체가 빚어지는 구간(초, 처음에 빠르게 채워지다 끝으로 갈수록 느려짐)과 입자가 모여드는 구간
        private const float FormStartSecs = 0.3f;
        private const float FormEndSecs = 4.6f;
        private const float WispEndSecs = 4.5f;
        private const float RevealStartSecs = 4.4f;
        private const float HoldReveal = 0.3f;
        private const float PushRevealSecs = 0.35f;

        // 형체는 꽃봉오리 위 허공에 떠서 천천히 오르내린다.
        private const float FormHeight = 2.6f;
        private const float BobAmplitude = 0.15f;
        private const float BobSpeed = 2.2f;
        private const float GatherStartScale = 0.92f;

        // 검은 입자: 초당 생성 수가 66에서 216으로 늘고, 반경 4.5~10.5칸에서 0.82초 만에 몸에 닿는다(플렉 수명 0.95초).
        private const float WispRateStart = 1.1f;
        private const float WispRateEnd = 3.6f;
        private static readonly FloatRange WispRadius = new FloatRange(4.5f, 10.5f);
        private const float WispTravelSecs = 0.82f;
        private const float SparkChance = 0.2f;

        // 꽃봉오리로 모이는 금빛 입자
        private static readonly FloatRange ChargeRadius = new FloatRange(2.5f, 6f);

        // 맵 전체 파장: 꽃봉오리에서 가장 먼 맵 모서리를 덮도록 키운다. 파면 반지름은 VV_ShockWave 셰이더와 같은 식으로 계산한다
        // (쿼드 반지름 × mix(0.02, 0.96, 1-(1-t)³), t = 경과 / _Duration). WaveDurationSecs는 XML VV_Fleck_ApocryphaRepelWave의 _Duration과 같게 둔다.
        private const float WaveDurationSecs = 2.6f;
        private const float WaveFrontStart = 0.02f;
        private const float WaveFrontReach = 0.96f;
        private const float WaveCoverMargin = 1.05f;

        // 파장 플렉은 만든 틱에 이미 한 틱만큼 나이를 먹고 그려진다(실측: 파면이 식보다 한 틱 앞섬).
        private const int WaveAgeLeadTicks = 1;

        // 형체는 꽃봉오리 위 허공(높이 FormHeight)에서 파면에 등을 떠밀려(몸 중심이 파면보다 몸 크기의 30%만큼 앞) 꽃봉오리에서 곧장 바깥으로
        // 등장 칸까지 날아가며 높이를 잃고, 거리에 비례해 살짝 떠올랐다 내려앉는다.
        private const float PushLeadFactor = 0.3f;
        private const float PushArcFactor = 0.06f;
        private const float PushArcMax = 5f;

        private static readonly Color DarkSparkColor = new Color(0.9f, 0.18f, 0.7f, 1f);
        private static readonly Color GoldSparkColor = new Color(1f, 0.86f, 0.45f, 1f);
        private static readonly int ProgressId = Shader.PropertyToID("_Progress");
        private static readonly int RevealId = Shader.PropertyToID("_Reveal");
        private static readonly MaterialPropertyBlock PropertyBlock = new MaterialPropertyBlock();
        private static readonly Dictionary<string, Material> MaterialCache = new Dictionary<string, Material>();

        private int _startTick = -1;
        private string _texPath;
        private float _drawSize = 6.5f;
        private Vector3 _budPos;
        private Vector3 _formPos;
        private Vector3 _landPos;
        private Rot4 _landRot = Rot4.South;
        private float _waveHalfSize = 100f;
        private int _pushTicks = 60;
        private bool _completed;

        // 저장하지 않는 연출 상태
        private Sustainer _gatherSustainer;

        private int Age => _startTick < 0 ? 0 : Mathf.Max(0, Find.TickManager.TicksGame - _startTick);
        private float AgeSecs => Age / 60f;
        private int LandTick => WaveTick + _pushTicks;

        // 실제 보스를 스폰할 틱(형체가 외곽에 내려앉는 순간).
        public int SpawnTick => _startTick + LandTick;

        // 영원꽃 위에서 연출을 시작한다. landCell은 보스가 실제로 스폰될 칸(맵 외곽), landRot은 그때의 방향.
        public static ApocryphaManifestation Begin(ArcanePlant_Everflower everflower, IntVec3 landCell, Rot4 landRot)
        {
            var map = everflower?.Map;
            if (map == null || !landCell.InBounds(map)) { return null; }

            var manifestation = (ApocryphaManifestation)ThingMaker.MakeThing(ApocryphaThingDefOf.VV_ApocryphaManifestation);
            manifestation.Init(everflower, landCell, landRot);
            GenSpawn.Spawn(manifestation, everflower.Position, map, WipeMode.Vanish);
            return manifestation;
        }

        private void Init(ArcanePlant_Everflower everflower, IntVec3 landCell, Rot4 landRot)
        {
            var graphicData = ApocryphaPawnKindDefOf.VV_Apocrypha.lifeStages[0].bodyGraphicData;
            _texPath = graphicData.texPath;
            _drawSize = graphicData.drawSize.x;

            var map = everflower.Map;
            _budPos = everflower.DrawPos + (everflower.EverflowerComp?.RitualEffecterOffset ?? Vector3.zero);
            _budPos.y = 0f;
            _formPos = _budPos + new Vector3(0f, 0f, FormHeight);
            _landPos = landCell.ToVector3Shifted();
            _landPos.y = 0f;
            _landRot = landRot;

            var farthest = 0f;
            foreach (var corner in new[] { new Vector3(0f, 0f, 0f), new Vector3(map.Size.x, 0f, 0f), new Vector3(0f, 0f, map.Size.z), new Vector3(map.Size.x, 0f, map.Size.z) })
            {
                farthest = Mathf.Max(farthest, Vector3.Distance(_budPos, corner));
            }
            _waveHalfSize = farthest * WaveCoverMargin / WaveFrontReach;

            // 파면이 (착지 거리 - 앞섬)에 닿는 시간을 파면 식에서 거꾸로 구해 착지 틱을 정한다.
            var front = Vector3.Distance(_budPos, _landPos) - PushLead;
            var u = Mathf.Clamp01((front / _waveHalfSize - WaveFrontStart) / (WaveFrontReach - WaveFrontStart));
            var t = 1f - Mathf.Pow(1f - u, 1f / 3f);
            _pushTicks = Mathf.Max(2, Mathf.CeilToInt(t * WaveDurationSecs * 60f) - WaveAgeLeadTicks);
        }

        private float PushLead => _drawSize * PushLeadFactor;

        public override void SpawnSetup(Map map, bool respawningAfterLoad)
        {
            base.SpawnSetup(map, respawningAfterLoad);

            if (!respawningAfterLoad && _startTick < 0)
            {
                _startTick = Find.TickManager.TicksGame;
            }
        }

        public override void DeSpawn(DestroyMode mode = DestroyMode.Vanish)
        {
            EndSustainer();
            base.DeSpawn(mode);
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref _startTick, "startTick", -1);
            Scribe_Values.Look(ref _texPath, "texPath");
            Scribe_Values.Look(ref _drawSize, "drawSize", 6.5f);
            Scribe_Values.Look(ref _budPos, "budPos", Vector3.zero);
            Scribe_Values.Look(ref _formPos, "formPos", Vector3.zero);
            Scribe_Values.Look(ref _landPos, "landPos", Vector3.zero);
            Scribe_Values.Look(ref _landRot, "landRot", Rot4.South);
            Scribe_Values.Look(ref _waveHalfSize, "waveHalfSize", 100f);
            Scribe_Values.Look(ref _pushTicks, "pushTicks", 60);
            Scribe_Values.Look(ref _completed, "completed", false);
        }

        protected override void Tick()
        {
            base.Tick();
            if (!Spawned) { return; }

            var age = Age;
            if (age < CompleteTick)
            {
                TickGather(age);
            }
            else if (age < WaveTick)
            {
                EndSustainer();
                if (age == CompleteTick)
                {
                    OnFormed();
                }
                ThrowChargeSparks();
            }
            else if (age < LandTick)
            {
                if (age == WaveTick)
                {
                    OnRepel();
                }
                ThrowPushTrail(age);
            }
            else
            {
                // 보스는 같은 틱에 퀘스트가 이 자리에 스폰한다(퀘스트가 먼저 돌았으면 이미 지워져 여기 오지 않는다).
                Land();
                Destroy();
            }
        }

        // 착지 연출(한 번만). 같은 틱에 퀘스트가 이 연출보다 먼저 돌며 보스를 스폰할 수 있으므로 퀘스트도 지우기 전에 부른다.
        public void Land()
        {
            if (_completed || !Spawned) { return; }

            _completed = true;
            OnLanded();
        }

        // 1) 모임: 검은 입자가 형체 쪽으로 모여든다(점점 빨라짐). 소리는 형체가 다 빚어질 때까지 이어진다.
        private void TickGather(int age)
        {
            var map = Map;
            if (_gatherSustainer == null || _gatherSustainer.Ended)
            {
                _gatherSustainer = ApocryphaSoundDefOf.PsychicArtifactWarmupSustained.TrySpawnSustainer(
                    SoundInfo.InMap(new TargetInfo(_formPos.ToIntVec3().ClampInsideMap(map), map), MaintenanceType.PerTick));
            }
            _gatherSustainer?.Maintain();

            var secs = age / 60f;
            if (secs >= WispEndSecs || !_formPos.ShouldSpawnMotesAt(map)) { return; }

            var rate = Mathf.Lerp(WispRateStart, WispRateEnd, Mathf.Pow(secs / WispEndSecs, 1.4f));
            var count = Mathf.FloorToInt(rate) + (Rand.Value < rate - Mathf.Floor(rate) ? 1 : 0);
            for (int i = 0; i < count; i++)
            {
                // 몸 둘레(타원) 안의 한 점을 노리고, 그 점에서 사방 4.5~10.5칸 떨어진 곳에서 출발한다.
                var target = _formPos + new Vector3(Rand.Range(-0.22f, 0.22f) * _drawSize, 0f, Rand.Range(-0.3f, 0.32f) * _drawSize);
                var offset = Quaternion.AngleAxis(Rand.Range(0f, 360f), Vector3.up) * Vector3.forward * WispRadius.RandomInRange;
                ThrowConverging(ApocryphaFleckDefOf.VV_Fleck_ApocryphaDarkWisp, target + offset, target, WispTravelSecs, Rand.Range(0.7f, 1.3f), null, alignToVelocity: true);

                if (Rand.Chance(SparkChance))
                {
                    ThrowConverging(ApocryphaFleckDefOf.VV_Fleck_ApocryphaManifestSpark, target + offset, target, WispTravelSecs, Rand.Range(0.25f, 0.45f), DarkSparkColor, alignToVelocity: false);
                }
            }
        }

        // 2) 형체가 다 빚어지는 순간: 자홍 섬광·굴절 맥동, 꽃봉오리에 금빛이 차오르기 시작한다.
        private void OnFormed()
        {
            var map = Map;
            ApocryphaEffecterDefOf.VV_Effecter_ApocryphaManifestComplete.Spawn(Position, map, _formPos - Position.ToVector3Shifted()).Cleanup();
            MoteMaker.MakeStaticMote(_budPos, map, ApocryphaThingDefOf.VV_Mote_ApocryphaRepelCharge, 1f, makeOffscreen: true);
            ApocryphaUtility.ShakeCamera(map, 0.4f);
        }

        private void ThrowChargeSparks()
        {
            if (!_budPos.ShouldSpawnMotesAt(Map)) { return; }

            for (int i = 0; i < 2; i++)
            {
                var offset = Quaternion.AngleAxis(Rand.Range(0f, 360f), Vector3.up) * Vector3.forward * ChargeRadius.RandomInRange;
                ThrowConverging(ApocryphaFleckDefOf.VV_Fleck_ApocryphaManifestSpark, _budPos + offset, _budPos, WispTravelSecs, Rand.Range(0.3f, 0.55f), GoldSparkColor, alignToVelocity: false);
            }
        }

        // 3) 파장: 꽃봉오리의 금빛 폭발 + 맵 전체로 퍼지는 금빛 파장. 형체는 이 순간부터 외곽으로 밀려난다.
        private void OnRepel()
        {
            var map = Map;
            ApocryphaEffecterDefOf.VV_Effecter_ApocryphaRepel.Spawn(Position, map, _budPos - Position.ToVector3Shifted()).Cleanup();

            var waveScale = _waveHalfSize * 2f / ApocryphaFleckDefOf.VV_Fleck_ApocryphaRepelWave.graphicData.drawSize.x;
            var data = FleckMaker.GetDataStatic(_budPos, map, ApocryphaFleckDefOf.VV_Fleck_ApocryphaRepelWave, waveScale);
            data.rotation = Rand.Range(0f, 360f);
            map.flecks.CreateFleck(data);

            ApocryphaUtility.ShakeCamera(map, 2.5f);
            Find.TickManager.slower.SignalForceNormalSpeedShort();
        }

        // 밀려나는 형체 뒤로 검은 입자가 흩날린다(형체가 화면에 있을 때만).
        private void ThrowPushTrail(int age)
        {
            var map = Map;
            var center = BodyPosAt(age);
            if (!center.ShouldSpawnMotesAt(map)) { return; }

            var back = (_formPos - _landPos).AngleFlat();
            for (int i = 0; i < 2; i++)
            {
                var pos = center + new Vector3(Rand.Range(-0.25f, 0.25f) * _drawSize, 0f, Rand.Range(-0.3f, 0.3f) * _drawSize);
                var data = FleckMaker.GetDataStatic(pos, map, ApocryphaFleckDefOf.VV_Fleck_ApocryphaDarkWisp, Rand.Range(0.8f, 1.4f));
                data.velocityAngle = back + Rand.Range(-20f, 20f);
                data.velocitySpeed = Rand.Range(2f, 5f);
                data.rotation = data.velocityAngle;
                map.flecks.CreateFleck(data);
            }
        }

        // 4) 착지: 흙먼지와 함께 검은 입자가 사방으로 튄다. 같은 틱에 퀘스트가 실제 보스를 스폰한다.
        private void OnLanded()
        {
            var map = Map;
            var cell = _landPos.ToIntVec3();
            if (!cell.InBounds(map)) { return; }

            ApocryphaEffecterDefOf.VV_Effecter_ApocryphaManifestLand.Spawn(cell, map).Cleanup();
            if (_landPos.ShouldSpawnMotesAt(map))
            {
                for (int i = 0; i < 30; i++)
                {
                    var data = FleckMaker.GetDataStatic(_landPos + Quaternion.AngleAxis(Rand.Range(0f, 360f), Vector3.up) * Vector3.forward * Rand.Range(0.3f, 1.6f), map,
                        ApocryphaFleckDefOf.VV_Fleck_ApocryphaDarkWisp, Rand.Range(0.8f, 1.4f));
                    data.velocityAngle = (data.spawnPosition - _landPos).AngleFlat();
                    data.velocitySpeed = Rand.Range(3f, 6f);
                    data.rotation = data.velocityAngle;
                    map.flecks.CreateFleck(data);
                }
            }

            ApocryphaUtility.ShakeCamera(map, 1.5f);
        }

        // start에서 target으로 travelSecs 만에 닿도록 날린다. alignToVelocity면 혜성 머리가 진행 방향을 향한다.
        private void ThrowConverging(FleckDef def, Vector3 start, Vector3 target, float travelSecs, float scale, Color? color, bool alignToVelocity)
        {
            var delta = target - start;
            delta.y = 0f;
            var data = FleckMaker.GetDataStatic(start, Map, def, scale);
            data.velocityAngle = delta.AngleFlat();
            data.velocitySpeed = delta.magnitude / travelSecs;
            data.rotation = alignToVelocity ? data.velocityAngle : Rand.Range(0f, 360f);
            if (color.HasValue)
            {
                data.instanceColor = color;
            }
            Map.flecks.CreateFleck(data);
        }

        private void EndSustainer()
        {
            if (_gatherSustainer != null && !_gatherSustainer.Ended)
            {
                _gatherSustainer.End();
            }
            _gatherSustainer = null;
        }

        #region 그리기
        // 몸통이 그려지는 중심(평면 좌표). 모임·완성 동안 허공에서 오르내리고, 파장 뒤에는 파면에 떠밀려(점점 느려지며) 외곽으로 날아가 살짝 떠올랐다 내려앉는다.
        private Vector3 BodyPosAt(float age)
        {
            if (age < WaveTick)
            {
                return _formPos + new Vector3(0f, 0f, BobAmplitude * Mathf.Sin(age / 60f * BobSpeed));
            }

            // 땅 위 위치는 꽃봉오리에서 등장 칸 쪽으로 파면을 따라가고, 떠 있던 높이(화면 위쪽 오프셋)는 내려앉으며 사라진다.
            var toLand = _landPos - _budPos;
            var landDist = toLand.magnitude;
            var s = 1f;
            if (age < LandTick && landDist > 0.5f)
            {
                s = Mathf.Clamp01((WaveFrontAt((age - WaveTick + WaveAgeLeadTicks) / 60f) + PushLead) / landDist);
            }

            var height = (FormHeight + BobAmplitude * Mathf.Sin(WaveTick / 60f * BobSpeed)) * (1f - s)
                + Mathf.Min(PushArcMax, landDist * PushArcFactor) * 4f * s * (1f - s);
            return _budPos + toLand * s + new Vector3(0f, 0f, height);
        }

        // 파장이 터진 지 secs초 뒤 파면의 반지름(칸)
        private float WaveFrontAt(float secs)
        {
            var t = Mathf.Clamp01(secs / WaveDurationSecs);
            return _waveHalfSize * Mathf.Lerp(WaveFrontStart, WaveFrontReach, 1f - Mathf.Pow(1f - t, 3f));
        }

        private float FormProgress(float secs)
        {
            var t = Mathf.InverseLerp(FormStartSecs, FormEndSecs, secs);
            return t <= 0f ? 0f : 1f - Mathf.Pow(1f - t, 2.2f);
        }

        private float Reveal(float age)
        {
            if (age < WaveTick)
            {
                return HoldReveal * Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(RevealStartSecs, CompleteTick / 60f, age / 60f));
            }

            return Mathf.Lerp(HoldReveal, 1f, Mathf.Clamp01((age - WaveTick) / 60f / PushRevealSecs));
        }

        protected override void DrawAt(Vector3 drawLoc, bool flip = false)
        {
            if (_texPath.NullOrEmpty() || _startTick < 0) { return; }

            var age = Age;
            var secs = age / 60f;
            var progress = FormProgress(secs);
            if (progress <= 0f) { return; }

            var rot = age < WaveTick ? Rot4.South : _landRot;
            var suffix = rot == Rot4.North ? "_north" : (rot == Rot4.South ? "_south" : "_east");
            var material = GetMaterial(_texPath + suffix);
            if (material == null) { return; }

            var pos = BodyPosAt(age);
            pos.y = def.altitudeLayer.AltitudeFor();

            var scale = _drawSize * (age < WaveTick ? Mathf.Lerp(GatherStartScale, 1f, progress) : 1f);

            PropertyBlock.Clear();
            PropertyBlock.SetFloat(ShaderPropertyIDs.AgeSecs, secs);
            PropertyBlock.SetFloat(ProgressId, progress);
            PropertyBlock.SetFloat(RevealId, Reveal(age));

            // 서쪽은 동쪽 텍스처를 좌우 반전해 그린다(Graphic_Multi와 같은 규칙).
            var mesh = rot == Rot4.West ? MeshPool.plane10Flip : MeshPool.plane10;
            var matrix = Matrix4x4.TRS(pos, Quaternion.identity, new Vector3(scale, 1f, scale));
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
                material = MaterialPool.MatFrom(texture, ShaderDatabase.LoadShader("Map/VV_Manifest"), Color.white);
                if (noise != null)
                {
                    material.SetTexture("_DissolveTex", noise);
                }
            }

            MaterialCache[fullPath] = material;
            return material;
        }
        #endregion
    }
}
