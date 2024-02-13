using RimWorld;
using Verse;

namespace VVRace
{
    public class PurifimintShield : ThingWithComps
    {
        private CompProjectileInterceptor _compProjectileInterceptor;

        public override void SpawnSetup(Map map, bool respawningAfterLoad)
        {
            base.SpawnSetup(map, respawningAfterLoad);

            _compProjectileInterceptor = GetComp<CompProjectileInterceptor>();
        }

        public override void Draw()
        {
            Comps_PostDraw();
        }

        public override void Tick()
        {
            base.Tick();

            if (_compProjectileInterceptor.currentHitPoints <= 0)
            {
                Destroy();
            }
        }
    }
}
