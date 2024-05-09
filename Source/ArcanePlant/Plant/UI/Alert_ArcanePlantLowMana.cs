using RimWorld;
using System.Collections.Generic;
using Verse;

namespace VVRace
{
    public class Alert_ArcanePlantLowMana : Alert_Critical
    {
        private List<Thing> dangerTargets = new List<Thing>();

        private List<Thing> DangerTargets
        {
            get
            {
                dangerTargets.Clear();

                var map = Find.CurrentMap;
                if (map != null)
                {
                    var candidates = ((WorkGiver_FertilizeArcanePlant)VVWorkGiverDefOf.VV_FertilizeArcanePlant.Worker).GetCachedCandidates(map);
                    if (candidates != null)
                    {
                        dangerTargets.AddRange(candidates);
                    }
                }

                return dangerTargets;
            }
        }

        public Alert_ArcanePlantLowMana()
        {
            defaultLabel = LocalizeString_Alert.VV_Alert_ArcanePlantLowMana.Translate();
            defaultPriority = AlertPriority.Critical;
        }

        public override AlertReport GetReport()
        {
            var dangerTargets = DangerTargets;
            if (dangerTargets.NullOrEmpty())
            {
                return false;
            }

            return AlertReport.CulpritsAre(dangerTargets);
        }

        public override TaggedString GetExplanation()
        {
            return LocalizeString_Alert.VV_Alert_ArcanePlantLowManaDesc.Translate();
        }
    }
}
