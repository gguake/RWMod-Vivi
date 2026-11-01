using RimWorld;
using Verse;

namespace VVRace
{
    public class QuestPart_ViviCalmBegin : QuestPart
    {
        public string inSignal;
        public ArcanePlant_Everflower anchor;

        public override void Notify_QuestSignalReceived(Signal signal)
        {
            base.Notify_QuestSignalReceived(signal);
            if (signal.tag != inSignal) { return; }

            var map = anchor != null && !anchor.Destroyed ? anchor.MapHeld : null;
            if (map == null) { return; }

            ViviEndingUtility.RunStep("calm: purge queued threats", () => ViviEndingUtility.PurgeQueuedThreats(map));
            ViviEndingUtility.RunStep("calm: hornets vanish", () => ViviEndingUtility.VanishWildTitanicHornets(map));
            ViviEndingUtility.RunStep("calm: raiders retreat", () => ViviEndingUtility.MakeRaidersRetreat(map));
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref inSignal, "inSignal");
            Scribe_References.Look(ref anchor, "anchor");
        }
    }
}
