using RimWorld;
using Verse;

namespace VVRace
{
    public class BatteryFlower : ArtificialPlant
    {
        public override void PreApplyDamage(ref DamageInfo dinfo, out bool absorbed)
        {
            base.PreApplyDamage(ref dinfo, out absorbed);

            if (Spawned && !Destroyed && dinfo.Amount > 0f && !absorbed)
            {
                var data = ArtificialPlantModExtension;
                if (data.empExplosiveMinimumEnergy >= 0 && Energy <= data.empExplosiveMinimumEnergy) { return; }

                var radius = data.empExplosiveRadiusRange.LerpThroughRange(EnergyChargeRatio);

                GenExplosion.DoExplosion(
                    instigator: this,
                    center: Position,
                    map: Map,
                    radius: radius,
                    damType: DamageDefOf.EMP);

                AddEnergy(-data.empExplosiveEnergy);
            }
        }
    }
}
