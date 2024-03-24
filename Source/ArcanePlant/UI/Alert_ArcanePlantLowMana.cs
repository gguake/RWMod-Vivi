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
                    foreach (var building in map.listerThings.ThingsInGroup(ThingRequestGroup.WithCustomRectForSelector))
                    {
                        if (building is ArcanePlant plant && plant.Faction == Faction.OfPlayer && plant.ManaChargeRatio <= 0.05f)
                        {
                            dangerTargets.Add(building);
                        }
                    }
                }

                return dangerTargets;
            }
        }

        public Alert_ArcanePlantLowMana()
        {
            defaultLabel = LocalizeTexts.AlertArcanePlantLowMana.Translate();
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
            return LocalizeTexts.AlertArcanePlantLowManaDesc.Translate();
        }
    }
}
