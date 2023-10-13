using RimWorld;
using System.Collections.Generic;
using System.Linq;
using Verse;

namespace VVRace
{
    public class Alert_LowLoyalty : Alert_Critical
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
                    foreach (var pawn in map.mapPawns.FreeColonistsSpawned)
                    {
                        var need = pawn.needs.TryGetNeed<Need_Loyalty>();
                        if (need != null && need.IsCritical)
                        {
                            dangerTargets.Add(pawn);
                        }
                    }
                }

                return dangerTargets;
            }
        }

        public Alert_LowLoyalty()
        {
            defaultLabel = LocalizeTexts.AlertViviLowLoyalty.Translate();
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
            return LocalizeTexts.AlertViviLowLoyaltyDesc.Translate();
        }
    }
}
