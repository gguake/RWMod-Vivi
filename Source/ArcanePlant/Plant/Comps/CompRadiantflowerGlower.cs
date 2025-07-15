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
                if (!parent.Position.Roofed(parent.Map)) { return false; }

                return true;
            }
        }
    }
}
