using RimWorld;
using RimWorld.Planet;
using System;
using System.Collections.Generic;
using System.Linq;
using Verse;

namespace VVRace
{
    public class WorldObjectComp_ViviEndingRaidBeacon : WorldObjectComp
    {
        private static readonly IncidentTargetTagDef[] RaidBeaconTags = { IncidentTargetTagDefOf.Map_RaidBeacon };

        public override IEnumerable<IncidentTargetTagDef> IncidentTargetTags()
        {
            var quest = ViviEndingQuest.Current;
            if (quest != null && parent is MapParent mapParent && mapParent.HasMap && quest.IsRaidBeaconActive(mapParent.Map))
            {
                return RaidBeaconTags;
            }

            return Enumerable.Empty<IncidentTargetTagDef>();
        }
    }

    [StaticConstructorOnStartup]
    internal static class WorldObjectComp_ViviEndingRaidBeaconInjector
    {
        static WorldObjectComp_ViviEndingRaidBeaconInjector()
        {
            var compClass = typeof(WorldObjectComp_ViviEndingRaidBeacon);
            foreach (var def in DefDatabase<WorldObjectDef>.AllDefsListForReading)
            {
                try
                {
                    if (def.worldObjectClass == null || !typeof(MapParent).IsAssignableFrom(def.worldObjectClass)) { continue; }

                    if (def.comps == null)
                    {
                        def.comps = new List<WorldObjectCompProperties>();
                    }
                    else if (def.comps.Any(c => c?.compClass == compClass))
                    {
                        continue;
                    }

                    def.comps.Add(new WorldObjectCompProperties { compClass = compClass });
                }
                catch (Exception ex)
                {
                    Log.Error($"[ViViRace] failed to add the ending raid beacon comp to {def.defName}: {ex}");
                }
            }
        }
    }
}
