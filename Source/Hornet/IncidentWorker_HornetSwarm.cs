using RimWorld;
using System.Linq;
using Verse;

namespace VVRace
{
    public class IncidentWorker_HornetSwarm : IncidentWorker
    {
        private static SimpleCurve _curveChanceFactor = new SimpleCurve(new CurvePoint[]
        {
            new CurvePoint(0f, 1f),
            new CurvePoint(2.999f, 1f),
            new CurvePoint(3f, 2f),
            new CurvePoint(10f, 3.5f),
            new CurvePoint(20f, 4.5f),
            new CurvePoint(100f, 7f),
        });

        private static SimpleCurve _curvePointFactor = new SimpleCurve(new CurvePoint[]
        {
            new CurvePoint(0f, 1f),
            new CurvePoint(5f, 1f),
            new CurvePoint(15f, 1.5f),
            new CurvePoint(20f, 2.0f),
            new CurvePoint(100f, 2.5f),
        });

        private const int AnimalsStayDurationMin = 90000;
        private const int AnimalsStayDurationMax = 180000;

        public override float ChanceFactorNow(IIncidentTarget target)
        {
            var viviCount = target.PlayerPawnsForStoryteller.Count(v => v.IsVivi());
            return _curveChanceFactor.Evaluate(viviCount);
        }

        protected override bool CanFireNowSub(IncidentParms parms)
        {
            if (!base.CanFireNowSub(parms))
            {
                return false;
            }

            var map = (Map)parms.target;
            var viviCount = map.PlayerPawnsForStoryteller.Count(v => v.IsVivi());
            var points = parms.points * _curvePointFactor.Evaluate(viviCount);
            if (points < 1500) { return false; }

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

            var viviCount = map.PlayerPawnsForStoryteller.Count(v => v.IsVivi());
            var points = parms.points * _curvePointFactor.Evaluate(viviCount);

            var pawns = AggressiveAnimalIncidentUtility.GenerateAnimals(kindDef, map.Tile, points, parms.pawnCount);
            var rot = Rot4.FromAngleFlat((map.Center - spawnPosition).AngleFlat);

            for (int i = 0; i < pawns.Count; i++)
            {
                var pawn = pawns[i];
                var loc = CellFinder.RandomClosewalkCellNear(spawnPosition, map, 10);
                QuestUtility.AddQuestTag(GenSpawn.Spawn(pawn, loc, map, rot), parms.questTag);

                pawn.mindState.mentalStateHandler.TryStartMentalState(VVMentalStateDefOf.VV_HornetBerserk);
                pawn.mindState.exitMapAfterTick = Find.TickManager.TicksGame + Rand.Range(AnimalsStayDurationMin, AnimalsStayDurationMax);
            }

            SendStandardLetter(LocalizeString_Letter.VV_Letter_IncidentHornetSwarmArrivedLabel.Translate(), LocalizeString_Letter.VV_Letter_IncidentHornetSwarmArrived.Translate(), LetterDefOf.ThreatBig, parms, pawns[0]);
            Find.TickManager.slower.SignalForceNormalSpeedShort();
            return true;
        }
    }

    public class IncidentWorker_HornetSwarm_Vivi : IncidentWorker_HornetSwarm
    {
    }
}
