using RimWorld;
using UnityEngine;
using Verse;

namespace VVRace
{
    [StaticConstructorOnStartup]
    public class ViviShieldDome : Building
    {
        private const string HoneycombTexPath = "VFX/VV_HoneycombShield";

        private const float DrawScalePerRadius = 2f * 1.1601562f;

        private static readonly Material HoneycombMat = MaterialPool.MatFrom(HoneycombTexPath, ShaderDatabase.MoteGlow);
        private static readonly MaterialPropertyBlock PropertyBlock = new MaterialPropertyBlock();

        private Thing _anchor;
        private int _startTick = -1;
        private int _endTick = -1;

        private CompProjectileInterceptor _interceptor;
        private ShieldDomeExtension _extension;

        public CompProjectileInterceptor Interceptor => _interceptor ?? (_interceptor = GetComp<CompProjectileInterceptor>());

        private ShieldDomeExtension Extension => _extension ?? (_extension = def.GetModExtension<ShieldDomeExtension>() ?? new ShieldDomeExtension());

        public int TicksLeft => Mathf.Max(0, _endTick - Find.TickManager.TicksGame);

        public float Radius => Interceptor?.Props.radius ?? 0f;

        public void Init(Thing anchor, int durationTicks)
        {
            _anchor = anchor;
            _startTick = Find.TickManager.TicksGame;
            _endTick = _startTick + Mathf.Max(1, durationTicks);
        }

        public void ExtendTo(int durationTicks)
        {
            _endTick = Mathf.Max(_endTick, Find.TickManager.TicksGame + durationTicks);
        }

        public static ViviShieldDome FindFor(Map map, ThingDef def, Thing anchor)
        {
            if (map == null || def == null) { return null; }

            var things = map.listerThings.ThingsOfDef(def);
            for (int i = 0; i < things.Count; i++)
            {
                if (things[i] is ViviShieldDome dome && !dome.Destroyed && dome._anchor == anchor)
                {
                    return dome;
                }
            }

            return null;
        }

        public override void SpawnSetup(Map map, bool respawningAfterLoad)
        {
            base.SpawnSetup(map, respawningAfterLoad);

            if (!respawningAfterLoad)
            {
                SetFaction(Faction.OfPlayer);
                if (_startTick < 0)
                {
                    Init(null, 1);
                }
            }
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_References.Look(ref _anchor, "anchor");
            Scribe_Values.Look(ref _startTick, "startTick", -1);
            Scribe_Values.Look(ref _endTick, "endTick", -1);
        }

        protected override void Tick()
        {
            base.Tick();
            if (!Spawned) { return; }

            if (Find.TickManager.TicksGame >= _endTick || (_anchor != null && (!_anchor.Spawned || _anchor.Map != Map)))
            {
                Collapse();
            }
        }

        public void Collapse()
        {
            if (Destroyed) { return; }

            if (Spawned)
            {
                EffecterDefOf.Shield_Break.SpawnAttached(this, Map, Mathf.Min(Radius, 10f));
            }

            Destroy();
        }

        protected override void DrawAt(Vector3 drawLoc, bool flip = false)
        {
            var ext = Extension;
            var now = Find.TickManager.TicksGame;
            var fadeTicks = Mathf.Max(1f, ext.fadeSecs * 60f);
            var fade = Mathf.Clamp01((now - _startTick) / fadeTicks) * Mathf.Clamp01((_endTick - now) / fadeTicks);

            var color = ext.color;
            color.a = fade * (ext.idleAlpha + ext.pulseAlpha * Mathf.Sin(Time.realtimeSinceStartup * ext.pulseSpeed + thingIDNumber));
            if (color.a > 0f)
            {
                var pos = drawLoc;
                pos.y = AltitudeLayer.MoteOverhead.AltitudeFor() - 0.01f;
                var size = Radius * DrawScalePerRadius;

                PropertyBlock.Clear();
                PropertyBlock.SetColor(ShaderPropertyIDs.Color, color);
                Graphics.DrawMesh(MeshPool.plane10, Matrix4x4.TRS(pos, Quaternion.identity, new Vector3(size, 1f, size)), HoneycombMat, 0, null, 0, PropertyBlock);
            }

            Comps_PostDraw();
        }

        public override void Destroy(DestroyMode mode = DestroyMode.Vanish)
        {
            var allow = allowDestroyNonDestroyable;
            allowDestroyNonDestroyable = true;
            try
            {
                base.Destroy(DestroyMode.Vanish);
            }
            finally
            {
                allowDestroyNonDestroyable = allow;
            }
        }
    }
}
