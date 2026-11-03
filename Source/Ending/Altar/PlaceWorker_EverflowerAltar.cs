using RimWorld;
using System.Collections.Generic;
using Verse;

namespace VVRace
{
    // 제단 틀이 발판의 건물과 마법 식물을 해체하므로 영원꽃 둘레에는 제단을 건설할 수 없다.
    public class PlaceWorker_EverflowerAltar : PlaceWorker
    {
        public override bool IsBuildDesignatorVisible(BuildableDef def)
        {
            return ViviEndingQuest.Current?.IsAltarUnlocked == true;
        }

        public override AcceptanceReport AllowsPlacing(BuildableDef checkingDef, IntVec3 loc, Rot4 rot, Map map, Thing thingToIgnore = null, Thing thing = null)
        {
            if (!Building_EverflowerAltar.IsValidAltarMap(map))
            {
                return new AcceptanceReport(LocalizeString_Etc.VV_FailReason_AltarNotHomeMap.Translate());
            }

            if (checkingDef is ThingDef altarDef)
            {
                if (AnyOtherOfDef(map, altarDef, thingToIgnore, thing)
                    || AnyOtherOfDef(map, altarDef.blueprintDef, thingToIgnore, thing)
                    || AnyOtherOfDef(map, altarDef.frameDef, thingToIgnore, thing))
                {
                    return new AcceptanceReport(LocalizeString_Etc.VV_FailReason_AltarOnePerMap.Translate());
                }
            }

            foreach (var cell in GenAdj.OccupiedRect(loc, rot, checkingDef.Size))
            {
                if (!cell.InBounds(map)) { continue; }

                if (Building_EverflowerAltar.IsOnSubstructure(cell, map))
                {
                    return new AcceptanceReport(LocalizeString_Etc.VV_FailReason_AltarOnSubstructure.Translate());
                }

                var thingList = cell.GetThingList(map);
                for (int i = 0; i < thingList.Count; ++i)
                {
                    var other = thingList[i];
                    if (other == thingToIgnore || other == thing) { continue; }

                    var isArcanePlant = other is ArcanePlant
                        || (other is Blueprint_Install installBlueprint && installBlueprint.ThingToInstall is ArcanePlant);
                    if (isArcanePlant)
                    {
                        return new AcceptanceReport(LocalizeString_Etc.VV_FailReason_AltarBlockedByArcanePlant.Translate());
                    }
                }
            }

            return true;
        }

        private static bool AnyOtherOfDef(Map map, ThingDef def, Thing thingToIgnore, Thing thing)
        {
            if (def == null) { return false; }

            List<Thing> things = map.listerThings.ThingsOfDef(def);
            for (int i = 0; i < things.Count; ++i)
            {
                if (things[i] != thingToIgnore && things[i] != thing)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
