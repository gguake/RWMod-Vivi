using RimWorld;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;
using Verse.AI;

namespace VVRace
{
    // 세이브에 기록되는 값이므로 기존 값을 변경하면 안 된다.
    public enum HornetSummonRole : byte
    {
        Escort = 1,
        FlowerGuard = 2,
        Assault = 3,
    }

    // 보스전 거대 말벌 소환 연출: 땅(멸망꽃이면 꽃)에서 검은 기운이 피어올라 말벌 형체를 빚고, 다 빚어지면 실제 말벌로 바뀐다.
    // 아스포델 등장 연출(ApocryphaManifestation)의 VV_Manifest 재질과 검은 입자를 작은 규모로 다시 쓴다.
    [StaticConstructorOnStartup]
    public class HornetManifestation : Thing
    {
        public const int DurationTicks = 150;

        private const float FormStartSecs = 0.1f;
        private const float FormEndSecs = 1.85f;
        private const float RevealStartSecs = 1.75f;
        private const float RevealEndSecs = 2.45f;
        private const float WispEndSecs = 1.8f;
        private const float RiseCells = 0.35f;
        private const float GatherStartScale = 0.85f;

        private const float WispTravelSecs = 0.6f;
        private const float WispRateStart = 0.6f;
        private const float WispRateEnd = 1.8f;
        private static readonly FloatRange GroundWispRadius = new FloatRange(1.4f, 3f);
        private static readonly FloatRange FlowerWispRadius = new FloatRange(0.1f, 0.5f);
        private const float RiseChance = 0.5f;
        private const float SparkChance = 0.15f;

        private const int StaggerTicks = 14;
        private const float MinSpacing = 2.2f;

        private static readonly Color DarkSparkColor = new Color(0.9f, 0.18f, 0.7f, 1f);
        private static readonly int ProgressId = Shader.PropertyToID("_Progress");
        private static readonly int RevealId = Shader.PropertyToID("_Reveal");
        private static readonly MaterialPropertyBlock PropertyBlock = new MaterialPropertyBlock();

        private int _startTick = -1;
        private HornetSummonRole _role = HornetSummonRole.Assault;
        private Pawn _boss;
        private Thing _flower;
        private Vector3 _sourcePos;
        private bool _fromFlower;
        private Rot4 _rot = Rot4.South;
        private bool _begun;

        private int Age => _startTick < 0 ? -1 : Find.TickManager.TicksGame - _startTick;

        #region 소환

        public static void SummonAround(Pawn boss, int count, HornetSummonRole role)
        {
            if (boss == null || !boss.Spawned || count <= 0) { return; }

            var map = boss.Map;
            var radius = boss.TryGetComp<CompApocrypha>()?.Props.summonRadiusAroundBoss ?? new FloatRange(3.5f, 7f);
            var chosen = new List<IntVec3>();
            for (int i = 0; i < count; i++)
            {
                if (!TryFindSummonCell(map, boss.Position, radius, chosen, out var cell)) { break; }

                chosen.Add(cell);
                Begin(map, cell, cell.ToVector3Shifted(), fromFlower: false, FacingAway(boss.Position, cell), role, boss, null, i * StaggerTicks + Rand.Range(0, StaggerTicks));
            }
        }

        public static void SummonFromFlower(Building_DoomFlower flower, Pawn boss, int count)
        {
            if (flower == null || !flower.Spawned || count <= 0) { return; }

            var map = flower.Map;
            var source = flower.DrawPos;
            var chosen = new List<IntVec3>();
            for (int i = 0; i < count; i++)
            {
                if (!TryFindSummonCell(map, flower.Position, new FloatRange(1f, 2.5f), chosen, out var cell)) { break; }

                chosen.Add(cell);
                Begin(map, cell, source, fromFlower: true, FacingAway(flower.Position, cell), HornetSummonRole.FlowerGuard, boss, flower, i * StaggerTicks);
            }
        }

        public static void CancelAll(Map map)
        {
            if (map == null) { return; }

            var pending = new List<Thing>(map.listerThings.ThingsOfDef(VVThingDefOf.VV_HornetManifestation));
            for (int i = 0; i < pending.Count; i++)
            {
                if (!pending[i].Destroyed)
                {
                    pending[i].Destroy(DestroyMode.Vanish);
                }
            }
        }

        private static void Begin(Map map, IntVec3 cell, Vector3 source, bool fromFlower, Rot4 rot, HornetSummonRole role, Pawn boss, Thing flower, int delayTicks)
        {
            var manifestation = (HornetManifestation)ThingMaker.MakeThing(VVThingDefOf.VV_HornetManifestation);
            manifestation._role = role;
            manifestation._boss = boss;
            manifestation._flower = flower;
            manifestation._sourcePos = source.Yto0();
            manifestation._fromFlower = fromFlower;
            manifestation._rot = rot;
            manifestation._startTick = Find.TickManager.TicksGame + Mathf.Max(0, delayTicks);
            GenSpawn.Spawn(manifestation, cell, map, WipeMode.Vanish);
        }

        // 바깥으로 향하는 쪽으로 돌려 세운다. 실제 말벌도 같은 방향으로 스폰해 형체와 이어지게 한다.
        private static Rot4 FacingAway(IntVec3 center, IntVec3 cell)
        {
            return cell == center ? Rot4.South : Rot4.FromAngleFlat((cell - center).AngleFlat);
        }

        private static bool TryFindSummonCell(Map map, IntVec3 center, FloatRange radius, List<IntVec3> chosen, out IntVec3 result)
        {
            var traverse = TraverseParms.For(TraverseMode.PassDoors);
            bool Valid(IntVec3 c, bool spaced)
            {
                if (!c.InBounds(map) || !c.Standable(map) || c.Fogged(map)) { return false; }
                if (!c.InHorDistOf(center, radius.max) || c.InHorDistOf(center, radius.min)) { return false; }
                if (c.GetFirstPawn(map) != null) { return false; }
                if (spaced && chosen.Any(v => v.InHorDistOf(c, MinSpacing))) { return false; }

                return map.reachability.CanReach(center, c, PathEndMode.OnCell, traverse);
            }

            var range = Mathf.CeilToInt(radius.max);
            return CellFinder.TryFindRandomCellNear(center, map, range, c => Valid(c, true), out result)
                || CellFinder.TryFindRandomCellNear(center, map, range, c => Valid(c, false), out result);
        }

        #endregion

        public override void ExposeData()
        {
            base.ExposeData();

            if (Scribe.mode == LoadSaveMode.Saving)
            {
                if (_boss != null && _boss.Destroyed) { _boss = null; }
                if (_flower != null && _flower.Destroyed) { _flower = null; }
            }

            Scribe_Values.Look(ref _startTick, "startTick", -1);
            Scribe_Values.Look(ref _role, "role", HornetSummonRole.Assault);
            Scribe_References.Look(ref _boss, "boss");
            Scribe_References.Look(ref _flower, "flower");
            Scribe_Values.Look(ref _sourcePos, "sourcePos", Vector3.zero);
            Scribe_Values.Look(ref _fromFlower, "fromFlower", false);
            Scribe_Values.Look(ref _rot, "rot", Rot4.South);
            Scribe_Values.Look(ref _begun, "begun", false);
        }

        protected override void Tick()
        {
            base.Tick();
            if (!Spawned) { return; }

            var age = Age;
            if (age < 0) { return; }

            // 다른 물건의 틱 도중에 스폰되면 첫 틱이 다음 틱이라 나이 0을 건너뛸 수 있다.
            if (!_begun)
            {
                _begun = true;
                VVEffecterDefOf.VV_Effecter_HornetManifestBegin?.Spawn(_sourcePos.ToIntVec3(), Map, SourceOffset).Cleanup();
            }

            if (age >= DurationTicks)
            {
                Complete();
                return;
            }

            ThrowWisps(age / 60f);
        }

        private Vector3 SourceOffset => (_sourcePos - _sourcePos.ToIntVec3().ToVector3Shifted()).Yto0();

        private void Complete()
        {
            var map = Map;
            var cell = Position;
            Destroy(DestroyMode.Vanish);

            // 그사이 보스전이 끝났으면 형체만 흩어진다.
            if (!ApocryphaGimmicks.IsBossFightActive)
            {
                VVEffecterDefOf.VV_Effecter_ApocryphaVanish?.Spawn(cell, map).Cleanup();
                return;
            }

            if (!cell.Standable(map))
            {
                cell = CellFinder.StandableCellNear(cell, map, 3f);
                if (!cell.IsValid) { return; }
            }

            Pawn hornet;
            try
            {
                hornet = ApocryphaUtility.SpawnBossFightHornet(map, cell, _rot);
            }
            catch (System.Exception ex)
            {
                Log.Error($"[ViViRace] failed to summon titanic hornet: {ex}");
                return;
            }

            VVEffecterDefOf.VV_Effecter_HornetManifestComplete?.Spawn(cell, map).Cleanup();
            JoinLord(hornet);
        }

        // 지킬 대상이 이미 사라졌으면 정착지를 습격한다.
        private void JoinLord(Pawn hornet)
        {
            var bossActive = _boss != null && _boss.Spawned && !_boss.Dead && _boss.Map == hornet.Map
                && (_boss.TryGetComp<CompApocrypha>()?.IsActive ?? false);

            if (_role == HornetSummonRole.Escort && bossActive)
            {
                ApocryphaUtility.JoinEscort(_boss, hornet);
            }
            else if (_role == HornetSummonRole.FlowerGuard && bossActive && _flower != null && _flower.Spawned && _flower.Map == hornet.Map)
            {
                ApocryphaUtility.JoinFlowerGuard(_boss, _flower, hornet);
            }
            else
            {
                ApocryphaUtility.JoinHornetAssault(hornet.Map, new List<Pawn> { hornet });
            }
        }

        // 검은 기운은 원천에서 곧게 피어오르는 줄기와, 둘레에서 형체로 모여드는 입자로 나눠 날린다.
        private void ThrowWisps(float secs)
        {
            if (secs >= WispEndSecs) { return; }

            var map = Map;
            var body = BodyPos(secs);
            if (!body.ShouldSpawnMotesAt(map)) { return; }

            var rate = Mathf.Lerp(WispRateStart, WispRateEnd, secs / WispEndSecs);
            var count = Mathf.FloorToInt(rate) + (Rand.Value < rate - Mathf.Floor(rate) ? 1 : 0);
            var radius = _fromFlower ? FlowerWispRadius : GroundWispRadius;
            for (int i = 0; i < count; i++)
            {
                var target = body + new Vector3(Rand.Range(-0.25f, 0.25f), 0f, Rand.Range(-0.3f, 0.3f));
                var start = _sourcePos + Quaternion.AngleAxis(Rand.Range(0f, 360f), Vector3.up) * Vector3.forward * radius.RandomInRange;
                ThrowConverging(VVFleckDefOf.VV_Fleck_ApocryphaDarkWisp, start, target, Rand.Range(0.45f, 0.8f), null, alignToVelocity: true);

                if (Rand.Chance(SparkChance))
                {
                    ThrowConverging(VVFleckDefOf.VV_Fleck_ApocryphaManifestSpark, start, target, Rand.Range(0.2f, 0.35f), DarkSparkColor, alignToVelocity: false);
                }
            }

            if (Rand.Chance(RiseChance))
            {
                var data = FleckMaker.GetDataStatic(_sourcePos + Gen.RandomHorizontalVector(0.3f), map, VVFleckDefOf.VV_Fleck_ApocryphaDarkWisp, Rand.Range(0.5f, 0.9f));
                data.velocityAngle = Rand.Range(-15f, 15f);
                data.velocitySpeed = Rand.Range(1.2f, 2.4f);
                data.rotation = data.velocityAngle;
                map.flecks.CreateFleck(data);
            }
        }

        private void ThrowConverging(FleckDef def, Vector3 start, Vector3 target, float scale, Color? color, bool alignToVelocity)
        {
            var delta = (target - start).Yto0();
            var data = FleckMaker.GetDataStatic(start, Map, def, scale);
            data.velocityAngle = delta.AngleFlat();
            data.velocitySpeed = delta.magnitude / WispTravelSecs;
            data.rotation = alignToVelocity ? data.velocityAngle : Rand.Range(0f, 360f);
            if (color.HasValue)
            {
                data.instanceColor = color;
            }
            Map.flecks.CreateFleck(data);
        }

        #region 그리기

        private static float FormProgress(float secs)
        {
            var t = Mathf.InverseLerp(FormStartSecs, FormEndSecs, secs);
            return t <= 0f ? 0f : 1f - Mathf.Pow(1f - t, 2f);
        }

        // 형체가 빚어지는 동안 조금 아래에서 떠올라 다 빚어지면 실제 말벌이 그려지는 칸 중심에 닿는다.
        private Vector3 BodyPos(float secs)
        {
            return DrawPos.Yto0() + new Vector3(0f, 0f, -RiseCells * (1f - FormProgress(secs)));
        }

        protected override void DrawAt(Vector3 drawLoc, bool flip = false)
        {
            var age = Age;
            if (age < 0) { return; }

            var secs = age / 60f;
            var progress = FormProgress(secs);
            if (progress <= 0f) { return; }

            var graphicData = VVPawnKindDefOf.VV_TitanicHornet.lifeStages[0].bodyGraphicData;
            var suffix = _rot == Rot4.North ? "_north" : (_rot == Rot4.South ? "_south" : "_east");
            var material = ApocryphaManifestation.GetMaterial(graphicData.texPath + suffix);
            if (material == null) { return; }

            var pos = BodyPos(secs);
            pos.y = AltitudeLayer.Pawn.AltitudeFor();
            var scale = graphicData.drawSize.x * Mathf.Lerp(GatherStartScale, 1f, progress);

            PropertyBlock.Clear();
            PropertyBlock.SetFloat(ShaderPropertyIDs.AgeSecs, secs);
            PropertyBlock.SetFloat(ProgressId, progress);
            PropertyBlock.SetFloat(RevealId, Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(RevealStartSecs, RevealEndSecs, secs)));

            var mesh = _rot == Rot4.West ? MeshPool.plane10Flip : MeshPool.plane10;
            var matrix = Matrix4x4.TRS(pos, Quaternion.identity, new Vector3(scale, 1f, scale));
            Graphics.DrawMesh(mesh, matrix, material, 0, null, 0, PropertyBlock);
        }

        #endregion
    }
}
