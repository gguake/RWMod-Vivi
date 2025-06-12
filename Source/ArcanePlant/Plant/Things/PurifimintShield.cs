using RimWorld;
using UnityEngine;
using Verse;

namespace VVRace
{
    public class PurifimintShield : ThingWithComps
    {
        private CompProjectileInterceptor _compProjectileInterceptor;

        public override int UpdateRateTicks => 1;

        public override void SpawnSetup(Map map, bool respawningAfterLoad)
        {
            base.SpawnSetup(map, respawningAfterLoad);

            _compProjectileInterceptor = GetComp<CompProjectileInterceptor>();
        }

        protected override void DrawAt(Vector3 drawLoc, bool flip = false)
        {
            Comps_PostDraw();
        }

        protected override void TickInterval(int delta)
        {
            if (_compProjectileInterceptor.currentHitPoints <= 0)
            {
                Destroy();
            }
        }
    }
}
