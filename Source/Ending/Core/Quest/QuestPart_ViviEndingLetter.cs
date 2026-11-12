using RimWorld;
using Verse;

namespace VVRace
{
    public class QuestPart_ViviEndingLetter : QuestPart
    {
        public string inSignal;
        public string labelKey;
        public string textKey;

        public LetterDef letterDef;
        public ArcanePlant_Everflower anchor;

        public override void Notify_QuestSignalReceived(Signal signal)
        {
            base.Notify_QuestSignalReceived(signal);
            if (signal.tag != inSignal) { return; }

            var text = ViviEndingUtility.TranslateWithResonator(textKey, ViviEndingUtility.ResonatorOf(anchor)).Resolve();

            Find.LetterStack.ReceiveLetter(
                labelKey.Translate(),
                text,
                letterDef ?? LetterDefOf.NeutralEvent,
                anchor != null && anchor.Spawned ? new LookTargets(anchor) : LookTargets.Invalid,
                null,
                quest);
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref inSignal, "inSignal");
            Scribe_Values.Look(ref labelKey, "labelKey");
            Scribe_Values.Look(ref textKey, "textKey");
            Scribe_Defs.Look(ref letterDef, "letterDef");
            Scribe_References.Look(ref anchor, "anchor");
        }
    }
}
