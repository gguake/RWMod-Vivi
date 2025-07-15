using RimWorld;
using Verse;

namespace VVRace
{
    public class CompProperties_StalitflowerGlower : CompProperties_Glower
    {
        public CompProperties_StalitflowerGlower()
        {
            compClass = typeof(CompStalitflowerGlower);
        }
    }

    public class CompStalitflowerGlower : CompManaGlower
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
