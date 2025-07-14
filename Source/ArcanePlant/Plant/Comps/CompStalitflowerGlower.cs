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
                if (!parent.Position.Roofed(parent.Map)) { return false; }

                return true;
            }
        }
    }
}
