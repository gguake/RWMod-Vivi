using RimWorld;
using System.Collections.Generic;
using Verse;

namespace VVRace
{
    public class Alert_DangerViviEgg : Alert_Critical
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
                    foreach (var egg in map.listerThings.ThingsOfDef(VVThingDefOf.VV_ViviEgg))
                    {
                        var compTemperature = egg.TryGetComp<CompTemperatureRuinable>();
                        if (compTemperature == null) { continue; }

                        if (egg.AmbientTemperature > compTemperature.Props.maxSafeTemperature ||
                            egg.AmbientTemperature < compTemperature.Props.minSafeTemperature)
                        {
                            dangerTargets.Add(egg);
                        }
                    }

                    foreach (var building in map.listerBuildings.AllBuildingsColonistOfDef(VVThingDefOf.VV_ViviHatchery))
                    {
                        var hatchery = building as ViviEggHatchery;
                        if (hatchery != null && hatchery.ViviEgg != null)
                        {
                            var egg = hatchery.ViviEgg;
                            var compTemperature = egg.TryGetComp<CompTemperatureRuinable>();
                            if (compTemperature == null) { continue; }

                            if (egg.AmbientTemperature > compTemperature.Props.maxSafeTemperature ||
                                egg.AmbientTemperature < compTemperature.Props.minSafeTemperature)
                            {
                                dangerTargets.Add(hatchery);
                            }
                        }

                    }
                }

                return dangerTargets;
            }
        }

        public Alert_DangerViviEgg()
        {
            defaultLabel = LocalizeString_Alert.VV_Alert_ViviDangerousEggs.Translate();
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
            var props = VVThingDefOf.VV_ViviEgg.GetCompProperties<CompProperties_TemperatureRuinable>();
            return LocalizeString_Alert.VV_Alert_ViviDangerousEggsDesc.Translate(
                props.minSafeTemperature.ToStringTemperature().Named("MINTEMP"), 
                props.maxSafeTemperature.ToStringTemperature().Named("MAXTEMP"));
        }
    }
}
