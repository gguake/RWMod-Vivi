using RimWorld.SketchGen;

namespace VVRace
{
    public class SketchResolver_ScoutingBasePowerRoom : SketchResolver
    {
        protected override bool CanResolveInt(ResolveParams parms)
        {
            if (parms.rect.HasValue)
            {
                return parms.sketch != null;
            }

            return false;
        }

        protected override void ResolveInt(ResolveParams parms)
        {
        }
    }
}
