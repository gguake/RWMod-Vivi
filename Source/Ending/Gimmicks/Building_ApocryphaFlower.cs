using RimWorld;
using UnityEngine;
using Verse;

namespace VVRace
{
    // 아포크리파가 씨앗으로 피워내는 꽃(멸망꽃/요정꽃) 공용 처리.
    // 피어날 때 작게 시작해 커지고, 이후 은은하게 숨을 쉬듯 크기가 변하며, 유지 이펙트를 매 틱 갱신한다.
    // 보스와 잇는 영혼 연결선은 ApocryphaGimmicks.DrawLinks가 이 꽃의 LinkAnchor/LinkAlpha를 읽어 그린다.
    public abstract class Building_ApocryphaFlower : Building
    {
        private const int BloomInTicks = 40;
        private const float LinkFadeInTicks = 45f;
        private const float BreathAmplitude = 0.025f;
        private const float BreathSpeed = 0.05f;

        protected int spawnTick = -1;

        [Unsaved]
        private Effecter _auraEffecter;

        [Unsaved]
        private ApocryphaFlowerExtension _extension;
        public ApocryphaFlowerExtension Extension => _extension ?? (_extension = def.GetModExtension<ApocryphaFlowerExtension>());

        // 보스와 잇는 연결선의 꽃 쪽 끝(꽃 머리)
        public Vector3 LinkAnchor => DrawPos + (Extension?.linkOffset ?? Vector3.zero);

        // 피어난 직후 연결선이 서서히 나타나도록
        public float LinkAlpha => spawnTick < 0 ? 1f : Mathf.Clamp01((Find.TickManager.TicksGame - spawnTick) / LinkFadeInTicks);

        public override void SpawnSetup(Map map, bool respawningAfterLoad)
        {
            base.SpawnSetup(map, respawningAfterLoad);

            if (!respawningAfterLoad)
            {
                spawnTick = Find.TickManager.TicksGame;
                ApocryphaGimmicks.PlayEffect(Extension?.bloomEffecter, Position, map);
            }
        }

        public override void DeSpawn(DestroyMode mode = DestroyMode.Vanish)
        {
            EndAura();
            base.DeSpawn(mode);
        }

        protected override void Tick()
        {
            base.Tick();

            if (Spawned)
            {
                MaintainAura();
            }
        }

        // 시들어 사라진다(이펙트와 함께). 멸망꽃의 만개·2페이즈 정리·요정꽃 결속/수명 종료 시 모두 이 메서드를 호출한다.
        public void Wither()
        {
            if (Destroyed) { return; }

            if (Spawned)
            {
                ApocryphaGimmicks.PlayEffect(Extension?.witherEffecter, Position, Map);
            }

            Destroy(DestroyMode.Vanish);
        }

        // 공격으로 파괴될 때도 시드는 이펙트를 보여 준다.
        public override void Kill(DamageInfo? dinfo = null, Hediff exactCulprit = null)
        {
            if (Spawned)
            {
                ApocryphaGimmicks.PlayEffect(Extension?.witherEffecter, Position, Map);
            }

            base.Kill(dinfo, exactCulprit);
        }

        protected override void DrawAt(Vector3 drawLoc, bool flip = false)
        {
            var ticksGame = Find.TickManager.TicksGame;
            var age = spawnTick < 0 ? BloomInTicks : ticksGame - spawnTick;

            // 피어나기: 0.3 → 1.0, 끝부분에서 살짝 부풀었다 돌아온다.
            var bloom = 1f;
            if (age < BloomInTicks)
            {
                var t = Mathf.Clamp01(age / (float)BloomInTicks);
                bloom = Mathf.Lerp(0.3f, 1f, t) + Mathf.Sin(t * Mathf.PI) * 0.12f;
            }

            var breath = 1f + BreathAmplitude * Mathf.Sin((ticksGame + thingIDNumber * 37) * BreathSpeed);
            var drawSize = def.graphicData.drawSize * (bloom * breath);

            var matrix = Matrix4x4.TRS(drawLoc, Quaternion.identity, new Vector3(drawSize.x, 1f, drawSize.y));
            Graphics.DrawMesh(MeshPool.plane10, matrix, Graphic.MatSingle, 0);

            Comps_DrawAt(drawLoc, flip);
            Comps_PostDraw();
        }

        private void MaintainAura()
        {
            var auraDef = Extension?.auraEffecter;
            if (auraDef == null) { return; }

            if (_auraEffecter == null)
            {
                _auraEffecter = auraDef.Spawn();
            }

            _auraEffecter.EffectTick(new TargetInfo(this), new TargetInfo(this));
        }

        private void EndAura()
        {
            if (_auraEffecter == null) { return; }

            _auraEffecter.Cleanup();
            _auraEffecter = null;
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref spawnTick, "spawnTick", -1);
        }
    }
}
