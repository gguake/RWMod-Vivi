using RimWorld.SketchGen;
using System;

namespace VVRace
{
    public class SketchResolver_ScoutingBaseArmoryRoom : SketchResolver
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
