using RimWorld;
using UnityEngine;
using Verse;

namespace VVRace
{
    public abstract class Building_ApocryphaFlower : Building
    {
        private const int BloomInTicks = 40;
        private const float LinkFadeInTicks = 45f;
        private const float BreathAmplitude = 0.025f;
        private const float BreathSpeed = 0.05f;

        protected int spawnTick = -1;

        [Unsaved]
        private ApocryphaFlowerExtension _extension;
        public ApocryphaFlowerExtension Extension => _extension ?? (_extension = def.GetModExtension<ApocryphaFlowerExtension>());

        public Vector3 LinkAnchor => DrawPos + (Extension?.linkOffset ?? Vector3.zero);

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

        public void Wither()
        {
            if (Destroyed) { return; }

            if (Spawned)
            {
                ApocryphaGimmicks.PlayEffect(Extension?.witherEffecter, Position, Map);
            }

            Destroy(DestroyMode.Vanish);
        }

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

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref spawnTick, "spawnTick", -1);
        }
    }
}
