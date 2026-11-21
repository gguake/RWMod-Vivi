using RimWorld;
using System.Collections.Generic;
using Verse;

namespace VVRace
{
    public class CompHornet : ThingComp
    {
        public CompProperties_Hornet Props => (CompProperties_Hornet)props;

        public override void PostPreApplyDamage(ref DamageInfo dinfo, out bool absorbed)
        {
            base.PostPreApplyDamage(ref dinfo, out absorbed);

            if (dinfo.Def?.armorCategory?.armorRatingStat == StatDefOf.ArmorRating_Heat)
            {
                dinfo.SetAmount(dinfo.Amount * 2f);
            }
        }

        public override IEnumerable<ThingDefCountClass> GetAdditionalLeavings(Map map, DestroyMode mode)
        {
            if (!(parent is Pawn pawn) || pawn.IsShambler || pawn.health.hediffSet.HasHediff(VVHediffDefOf.VV_DoomPhantasm)) { yield break; }
            if (Props.killedLeavingsRanges == null || !Rand.Chance(Props.killedLeavingsChance)) { yield break; }

            foreach (var range in Props.killedLeavingsRanges)
            {
                var count = range.countRange.RandomInRange;
                if (count > 0)
                {
                    yield return new ThingDefCountClass(range.thingDef, count);
                }
            }
        }
    }
}
