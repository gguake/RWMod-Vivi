using RimWorld;
using System.Collections.Generic;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace VVRace
{
    [StaticConstructorOnStartup]
    public class ApocryphaManifestation : Thing
    {
        private const int CompleteTick = 300;
        private const int WaveTick = 342;

        private const float FormStartSecs = 0.3f;
        private const float FormEndSecs = 4.6f;
        private const float WispEndSecs = 4.5f;
        private const float RevealStartSecs = 4.4f;
        private const float HoldReveal = 0.3f;
        private const float PushRevealSecs = 0.35f;

        private const float FormHeight = 2.6f;
        private const float BobAmplitude = 0.15f;
        private const float BobSpeed = 2.2f;
        private const float GatherStartScale = 0.92f;

        private const float WispRateStart = 1.1f;
        private const float WispRateEnd = 3.6f;
        private static readonly FloatRange WispRadius = new FloatRange(4.5f, 10.5f);
        private const float WispTravelSecs = 0.82f;
        private const float SparkChance = 0.2f;

        private static readonly FloatRange ChargeRadius = new FloatRange(2.5f, 6f);

        // 맵 끝까지 퍼지는 파면의 반지름은 VV_ShockWave와 동일하게 쿼드 반지름 × mix(0.02, 0.96, 1-(1-t)³)로 계산한다.
        // t는 경과 시간 / _Duration이며 WaveDurationSecs는 XML의 VV_Fleck_ApocryphaRepelWave._Duration과 맞춘다.
        private const float WaveDurationSecs = 2.6f;
        private const float WaveFrontStart = 0.02f;
        private const float WaveFrontReach = 0.96f;
        private const float WaveCoverMargin = 1.05f;

        // 생성된 플렉은 같은 틱에 나이가 1틱 증가하므로 파면이 계산식보다 1틱 앞선다.
        private const int WaveAgeLeadTicks = 1;

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
        private float _drawSize = 3f;
        private Vector3 _budPos;
        private Vector3 _formPos;
        private Vector3 _landPos;
        private Rot4 _landRot = Rot4.South;
        private float _waveHalfSize = 100f;
        private int _pushTicks = 60;
        private bool _completed;

        private Sustainer _gatherSustainer;

        private int Age => _startTick < 0 ? 0 : Mathf.Max(0, Find.TickManager.TicksGame - _startTick);
        private float AgeSecs => Age / 60f;
        private int LandTick => WaveTick + _pushTicks;

        public int SpawnTick => _startTick + LandTick;

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
            Scribe_Values.Look(ref _drawSize, "drawSize", 3f);
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
                Land();
                Destroy();
            }
        }

        // 퀘스트가 먼저 보스를 스폰할 수 있으므로 연출을 제거하기 전에 착지 효과를 호출한다. 중복 호출은 무시한다.
        public void Land()
        {
            if (_completed || !Spawned) { return; }

            _completed = true;
            OnLanded();
        }

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
                var target = _formPos + new Vector3(Rand.Range(-0.22f, 0.22f) * _drawSize, 0f, Rand.Range(-0.3f, 0.32f) * _drawSize);
                var offset = Quaternion.AngleAxis(Rand.Range(0f, 360f), Vector3.up) * Vector3.forward * WispRadius.RandomInRange;
                ThrowConverging(ApocryphaFleckDefOf.VV_Fleck_ApocryphaDarkWisp, target + offset, target, WispTravelSecs, Rand.Range(0.7f, 1.3f), null, alignToVelocity: true);

                if (Rand.Chance(SparkChance))
                {
                    ThrowConverging(ApocryphaFleckDefOf.VV_Fleck_ApocryphaManifestSpark, target + offset, target, WispTravelSecs, Rand.Range(0.25f, 0.45f), DarkSparkColor, alignToVelocity: false);
                }
            }
        }

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
        private Vector3 BodyPosAt(float age)
        {
            if (age < WaveTick)
            {
                return _formPos + new Vector3(0f, 0f, BobAmplitude * Mathf.Sin(age / 60f * BobSpeed));
            }

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
