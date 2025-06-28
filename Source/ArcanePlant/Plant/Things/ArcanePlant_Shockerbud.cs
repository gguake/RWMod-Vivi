using RimWorld;
using Verse;

namespace VVRace
{
    public class ArcanePlant_Shockerbud : ArcanePlant
    {
        public CompManaSensorExplosive CompSensorExplosive
        {
            get
            {
                if (_compSensorExplosive == null)
                {
                    _compSensorExplosive = this.TryGetComp<CompManaSensorExplosive>();
                }

                return _compSensorExplosive;
            }
        }
        private CompManaSensorExplosive _compSensorExplosive;

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
