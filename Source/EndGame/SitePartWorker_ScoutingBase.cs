using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace VVRace
{
    public class SitePartWorker_ScoutingBase : SitePartWorker
    {
        public static readonly SimpleCurve ThreatPointsLootMarketValue = new SimpleCurve
        {
            new CurvePoint(100f, 200f),
            new CurvePoint(250f, 450f),
            new CurvePoint(800f, 1000f),
            new CurvePoint(10000f, 2000f)
        };

        public override string GetPostProcessedThreatLabel(Site site, SitePart sitePart)
        {
            if (site.MainSitePartDef == def)
            {
                return null;
            }
            return base.GetPostProcessedThreatLabel(site, sitePart);
        }
    }
}
