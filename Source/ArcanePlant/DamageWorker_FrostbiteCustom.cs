using RimWorld;
using Verse;

namespace VVRace
{
    public class DamageWorker_FrostbiteCustom : DamageWorker_Frostbite
    {
        public override DamageResult Apply(DamageInfo dinfo, Thing thing)
        {
            var result = base.Apply(dinfo, thing);

            var fire = thing as Fire;
            if (fire == null || fire.Destroyed)
            {
                var attachedFire = thing.GetAttachment(ThingDefOf.Fire);
                if (attachedFire != null)
                { 
                    fire = (Fire)attachedFire;
                }
            }

            if (fire != null && !fire.Destroyed)
            {
                fire.fireSize -= dinfo.Amount * 0.01f;
                if (fire.fireSize < 0.1f)
                {
                    fire.Destroy();
                }
            }

            return result;
        }
    }
}
