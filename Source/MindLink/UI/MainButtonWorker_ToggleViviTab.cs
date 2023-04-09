using RimWorld;
using System.Linq;
using Verse;

namespace VVRace
{
    public class MainButtonWorker_ToggleViviTab : MainButtonWorker_ToggleTab
    {
        public override bool Disabled
        {
            get
            {
                if (base.Disabled) { return true; }

                var currentMap = Find.CurrentMap;
                if (currentMap != null)
                {
                    var spawnedViviExists = currentMap.mapPawns.SpawnedPawnsInFaction(Faction.OfPlayer).Any(p => p.IsMindLinkedVivi());
                    if (spawnedViviExists)
                    {
                        return false;
                    }

                    var viviExists = currentMap.mapPawns.PawnsInFaction(Faction.OfPlayer).Any(p => p.IsMindLinkedVivi());
                    if (viviExists)
                    {
                        return false;
                    }
                }

                return true;
            }
        }

        public override bool Visible => !Disabled;
    }
}
