using RimWorld;
using RimWorld.Planet;

namespace VVRace
{
    public class SitePartWorker_ScoutingBase : SitePartWorker
    {
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
