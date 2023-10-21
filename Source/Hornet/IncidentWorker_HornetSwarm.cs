using RimWorld;
using Verse;

namespace VVRace
{
    public class IncidentWorker_HornetSwarm : IncidentWorker
    {
        protected virtual float PointsFactor => 1f;
        private const int AnimalsStayDurationMin = 90000;
        private const int AnimalsStayDurationMax = 300000;

        protected override bool CanFireNowSub(IncidentParms parms)
        {
            if (!base.CanFireNowSub(parms))
            {
                return false;
            }
            var map = (Map)parms.target;
            return RCellFinder.TryFindRandomPawnEntryCell(out _, map, CellFinder.EdgeRoadChance_Animal);
        }

        protected override bool TryExecuteWorker(IncidentParms parms)
        {
            var map = (Map)parms.target;
            var kindDef = VVPawnKindDefOf.VV_TitanicHornet;

            var spawnPosition = parms.spawnCenter;
            if (!spawnPosition.IsValid && !RCellFinder.TryFindRandomPawnEntryCell(out spawnPosition, map, CellFinder.EdgeRoadChance_Animal))
            {
                return false;
            }

            var pawns = ManhunterPackIncidentUtility.GenerateAnimals(kindDef, map.Tile, parms.points * PointsFactor, parms.pawnCount);
            var rot = Rot4.FromAngleFlat((map.Center - spawnPosition).AngleFlat);

            for (int i = 0; i < pawns.Count; i++)
            {
                var pawn = pawns[i];
                var loc = CellFinder.RandomClosewalkCellNear(spawnPosition, map, 10);
                QuestUtility.AddQuestTag(GenSpawn.Spawn(pawn, loc, map, rot), parms.questTag);

                pawn.mindState.mentalStateHandler.TryStartMentalState(VVMentalStateDefOf.VV_HornetBerserk);
                pawn.mindState.exitMapAfterTick = Find.TickManager.TicksGame + Rand.Range(AnimalsStayDurationMin, AnimalsStayDurationMax);
            }

            SendStandardLetter(LocalizeTexts.LetterIncidentHornetSwarmArrivedLabel.Translate(), LocalizeTexts.LetterIncidentHornetSwarmArrived.Translate(), LetterDefOf.ThreatBig, parms, pawns[0]);
            Find.TickManager.slower.SignalForceNormalSpeedShort();
            return true;
        }
    }

    public class IncidentWorker_HornetSwarm_Vivi : IncidentWorker_HornetSwarm
    {
        protected override float PointsFactor => 1.3f;

        protected override bool CanFireNowSub(IncidentParms parms)
        {
            if (!base.CanFireNowSub(parms))
            {
                return false;
            }

            var map = (Map)parms.target;
            if (!map.ParentFaction.IsPlayer || !map.ParentFaction.def.allowedCultures.Contains(VVCultureDefOf.VV_ViviCulture))
            {
                return false;
            }

            return true;
        }
    }
}
