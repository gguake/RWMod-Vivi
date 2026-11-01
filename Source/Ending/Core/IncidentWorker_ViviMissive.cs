using RimWorld;
using Verse;

namespace VVRace
{
    public class IncidentWorker_ViviMissive : IncidentWorker_GiveQuest
    {
        protected override bool CanFireNowSub(IncidentParms parms)
        {
            return base.CanFireNowSub(parms) && ViviEndingQuest.MissiveArrivalConditionsMet();
        }

        protected override bool TryExecuteWorker(IncidentParms parms)
        {
            return ViviEndingQuest.Current?.StartMissiveQuest(null) == true;
        }
    }
}
