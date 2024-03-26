using RimWorld;
using Verse;

namespace VVRace
{
    public class ArcanePlant_Thunderbud : ArcanePlant
    {
        public CompSensorExplosive CompSensorExplosive
        {
            get
            {
                if (_compSensorExplosive == null)
                {
                    _compSensorExplosive = this.TryGetComp<CompSensorExplosive>();
                }

                return _compSensorExplosive;
            }
        }
        private CompSensorExplosive _compSensorExplosive;

        public override void PreApplyDamage(ref DamageInfo dinfo, out bool absorbed)
        {
            base.PreApplyDamage(ref dinfo, out absorbed);

            if (Spawned && !Destroyed && dinfo.Amount > 0f && !absorbed && !CompSensorExplosive.IsCooldown)
            {
                CompSensorExplosive.TryExplosive();
            }
        }
    }
}
