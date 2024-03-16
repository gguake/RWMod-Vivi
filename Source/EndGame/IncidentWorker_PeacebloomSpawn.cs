using RimWorld;
using Verse;

namespace VVRace
{
    public class IncidentWorker_PeacebloomSpawn : IncidentWorker
    {
        private IncidentDefPeacebloomSpawnExtension Extension
        {
            get
            {
                if (_extension == null)
                {
                    _extension = def.GetModExtension<IncidentDefPeacebloomSpawnExtension>();
                }

                return _extension;
            }
        }
        private IncidentDefPeacebloomSpawnExtension _extension = null;

        protected override bool CanFireNowSub(IncidentParms parms)
        {
            if (!CanFireNowSub(parms))
            {
                return false;
            }

            var extension = Extension;
            var map = (Map)parms.target;

            // 기지가치 체크
            if (extension.requiredPlayerWealth > 0f && WealthUtility.PlayerWealth < extension.requiredPlayerWealth) { return false; }

            var artificialPlantCount = 0;
            var artificialPlants = map.listerThings.ThingsInGroup(ThingRequestGroup.WithCustomRectForSelector);
            for (int i = 0; i < artificialPlants.Count; ++i)
            {
                if (artificialPlants[i] is ArtificialPlant)
                {
                    artificialPlantCount++;
                }
            }

            // 고대 식물 숫자 체크
            if (extension.requiredArtificialPlantCount > 0 && artificialPlantCount < extension.requiredArtificialPlantCount)
            {
                return false;
            }

            var freeColonists = map.mapPawns.FreeColonistsAndPrisonersSpawned;
            int freeColonistCount = freeColonists.Count, viviCount = 0, royalViviCount = 0;
            for (int i = 0; i < freeColonistCount; ++i)
            {
                var compVivi = freeColonists[i].GetCompVivi();
                if (compVivi != null)
                {
                    viviCount++;

                    if (compVivi.isRoyal)
                    {
                        royalViviCount++;
                    }
                }
            }

            // 비비 비율 체크
            var viviColonistRatio = (float)viviCount / freeColonistCount;
            if (extension.requiredViviColonistRatio > 0f && viviColonistRatio < extension.requiredViviColonistRatio)
            {
                return false;
            }

            // 로얄 비비 숫자 체크
            if (extension.requiredRoyalViviCount > 0 && royalViviCount < extension.requiredRoyalViviCount)
            {
                return false;
            }

            return true;
        }

        protected override bool TryExecuteWorker(IncidentParms parms)
        {
            var extension = Extension;
            var map = (Map)parms.target;
            if (!CellFinderLoose.TryFindRandomNotEdgeCellWith(10, c => CanSpawnAt(extension, map, c), map, out var cell))
            {
                return false;
            }

            cell.GetPlant(map)?.Destroy();
            var thing = GenSpawn.Spawn(extension.targetThingDef, cell, map);
            if (thing == null)
            {
                return false;
            }

            SendStandardLetter(parms, thing);
            return true;
        }

        private bool CanSpawnAt(IncidentDefPeacebloomSpawnExtension extension, Map map, IntVec3 c)
        {
            if (!c.Standable(map) || c.Fogged(map) || !c.GetRoom(map).PsychologicallyOutdoors || c.Roofed(map)) { return false; }

            var plant = c.GetPlant(map);
            if (plant != null && plant.def.plant.growDays > 10f) { return false; }

            var thingList = c.GetThingList(map);
            for (int i = 0; i < thingList.Count; ++i)
            {
                if (thingList[i].def == extension.targetThingDef)
                {
                    return false;
                }
            }

            if (!map.reachability.CanReachFactionBase(c, map.ParentFaction)) { return false; }

            if (c.GetTerrain(map).avoidWander) { return false; }

            if (!ArtificialPlantUtility.CanPlaceArtificialPlantToCell(map, c, extension.targetThingDef)) { return false; }

            return true;
        }
    }
}
