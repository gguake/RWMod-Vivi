using RimWorld;
using System.Collections.Generic;
using Verse;

namespace VVRace
{
    public class Alert_LowEnergyArtificialPlant : Alert
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
                    foreach (var building in map.listerThings.ThingsInGroup(ThingRequestGroup.BuildingArtificial))
                    {
                        if (building is ArtificialPlant plant && plant.EnergyChargeRatio <= 0.01f)
                        {
                            dangerTargets.Add(building);
                        }
                    }
                }

                return dangerTargets;
            }
        }

        public Alert_LowEnergyArtificialPlant()
        {
            defaultLabel = LocalizeTexts.AlertArtificialPlantLowEnergy.Translate();
            defaultPriority = AlertPriority.High;
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
            return LocalizeTexts.AlertArtificialPlantLowEnergyDesc.Translate();
        }
    }
}
