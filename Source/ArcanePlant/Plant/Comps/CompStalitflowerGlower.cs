using RimWorld;
using System.Collections.Generic;
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

                if (parent.Map.Tile.LayerDef == PlanetLayerDefOf.Orbit)
                {
                    return true;
                }
                else
                {
                    var dayPct = GenLocalDate.DayPercent(parent.Map);
                    return dayPct < 0.2f || dayPct > 0.7f;
                }
            }
        }

        public override IEnumerable<string> GetFunctionDescriptions()
        {
            yield return LocalizeString_PlantFunction.VV_PlantFunction_GlowStarlit.Translate(
                Props.glowRadius.ToString("0.#"));
        }
    }
}
