using RimWorld;
using Verse;

namespace VVRace
{
    public class CompViviHoneycombWall : ThingComp
    {
        public override void PostPreApplyDamage(ref DamageInfo dinfo, out bool absorbed)
        {
            absorbed = false;
            float amount = 0.75f * dinfo.Amount;
            if (dinfo.Def == DamageDefOf.Flame || dinfo.Def == DamageDefOf.Burn)
            {
                amount = 4f * dinfo.Amount;
            }

            dinfo.SetAmount(amount);
        }
    }
}
