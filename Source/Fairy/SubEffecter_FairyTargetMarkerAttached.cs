using RimWorld;
using UnityEngine;
using Verse;

namespace VVRace
{
    public class SubEffecter_FairyTargetMarkerAttached : SubEffecter
    {
        private Mote markerMote;

        public SubEffecter_FairyTargetMarkerAttached(SubEffecterDef def, Effecter parent) : base(def, parent)
        {
        }

        public override void SubEffectTick(TargetInfo A, TargetInfo B)
        {
            MaintainMarker(A);
        }

        public override void SubCleanup()
        {
            EndMarker();
        }

        private void MaintainMarker(TargetInfo target)
        {
            var thing = target.Thing;
            if (thing == null || thing.Destroyed || !thing.Spawned || thing.Map == null || def.moteDef == null)
            {
                EndMarker();
                return;
            }

            if (markerMote == null || markerMote.Destroyed || markerMote.Map != thing.Map)
            {
                markerMote = MoteMaker.MakeAttachedOverlay(
                    thing,
                    def.moteDef,
                    def.positionOffset,
                    MarkerScale(),
                    solidTimeOverride: -1f);
            }

            markerMote.Attach(new TargetInfo(thing));
            markerMote.Maintain();
        }

        private float MarkerScale()
        {
            var scale = def.scale.RandomInRange;
            return scale > 0f ? scale : 1f;
        }

        private void EndMarker()
        {
            if (markerMote == null) { return; }

            markerMote.Destroy();
            markerMote = null;
        }
    }
}
