using RimWorld;
using System.Collections.Generic;
using Verse;

namespace VVRace
{
    public class Alert_GrowingArcanePlantLowHP : Alert_Critical
    {
        private List<Thing> _dangerTargets = new List<Thing>();
        private List<Thing> DangerTargets
        {
            get
            {
                _dangerTargets.Clear();

                var map = Find.CurrentMap;
                if (map != null)
                {
                    foreach (var building in map.listerBuildings.AllBuildingsColonistOfDef(VVThingDefOf.VV_ArcanePlantFarm))
                    {
                        var farm = building as Building_ArcanePlantFarm;
                        if (farm != null && farm.Bill != null && farm.Bill.Stage == GrowingArcanePlantBillStage.Growing && farm.Bill.HealthPct < 0.333f)
                        {
                            _dangerTargets.Add(farm);
                        }
                    }
                }

                return _dangerTargets;
            }
        }

        public Alert_GrowingArcanePlantLowHP()
        {
            defaultLabel = LocalizeString_Alert.VV_Alert_GrowingArcanePlantLowHP.Translate();
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
            return LocalizeString_Alert.VV_Alert_GrowingArcanePlantLowHPDesc.Translate();
        }
    }
}
