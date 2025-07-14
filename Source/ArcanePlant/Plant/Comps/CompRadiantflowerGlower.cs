using RimWorld;
using Verse;

namespace VVRace
{
    public class CompProperties_RadiantflowerGlower : CompProperties_Glower
    {
        public CompProperties_RadiantflowerGlower()
        {
            compClass = typeof(CompRadiantflowerGlower);
        }
    }

    public class CompRadiantflowerGlower : CompManaGlower
    {
        protected override bool ShouldBeLitNow
        {
            get
            {
                if (!base.ShouldBeLitNow) { return false; }

                if (parent.Position.GetVacuum(parent.Map) < 0.5f)
                {
                    var dayPct = GenLocalDate.DayPercent(parent.Map);
                    return dayPct < 0.2f || dayPct > 0.7f;
                }

                return true;
            }
        }
    }
}
