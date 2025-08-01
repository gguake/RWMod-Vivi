using RimWorld;
using Verse;

namespace VVRace
{
    public class CompHornet : ThingComp
    {
        public override void PostPreApplyDamage(ref DamageInfo dinfo, out bool absorbed)
        {
            base.PostPreApplyDamage(ref dinfo, out absorbed);

            if (dinfo.Def?.armorCategory?.armorRatingStat == StatDefOf.ArmorRating_Heat)
            {
                dinfo.SetAmount(dinfo.Amount * 2f);
            }
        }
    }
}
