using RimWorld;
using System.Linq;
using Verse;
using Verse.AI.Group;

namespace VVRace
{
    public class IncidentWorker_HornetAssault : IncidentWorker
    {
        protected override bool CanFireNowSub(IncidentParms parms)
        {
            if (!base.CanFireNowSub(parms))
            {
                return false;
            }

            var map = (Map)parms.target;
            if (map.Biome.inVacuum || map.IsTempIncidentMap) { return false; }

            return true;
        }

        protected override bool TryExecuteWorker(IncidentParms parms)
        {
            parms.raidArrivalMode = PawnsArrivalModeDefOf.EdgeWalkIn;

            var map = (Map)parms.target;
            var kindDef = VVPawnKindDefOf.VV_TitanicHornet;

            var spawnPosition = parms.spawnCenter;
            if (!spawnPosition.IsValid && !RCellFinder.TryFindRandomPawnEntryCell(out spawnPosition, map, CellFinder.EdgeRoadChance_Animal))
            {
                return false;
            }

            var viviCount = map.PlayerPawnsForStoryteller.Count(v => v.IsVivi());
            var points = parms.points;

            var pawns = AggressiveAnimalIncidentUtility.GenerateAnimals(kindDef, map.Tile, points, parms.pawnCount);
            if (!parms.raidArrivalMode.Worker.TryResolveRaidSpawnCenter(parms))
            {
                return false;
            }

            parms.raidArrivalMode.Worker.Arrive(pawns, parms);
            LordMaker.MakeNewLord(null, new LordJob_HornetAssault(), parms.target as Map, pawns);

            SendStandardLetter(
                LocalizeString_Letter.VV_Letter_IncidentHornetAssaultArrivedLabel.Translate(), 
                LocalizeString_Letter.VV_Letter_IncidentHornetAssaultArrived.Translate(), 
                LetterDefOf.ThreatBig, 
                parms, 
                pawns[0]);

            Find.TickManager.slower.SignalForceNormalSpeedShort();
            return true;
        }
    }
}
