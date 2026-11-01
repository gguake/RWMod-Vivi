using RimWorld;
using Verse;

namespace VVRace
{
    // QuestPart_Letter는 생성 시 텍스트를 고정하므로 발송 시점에 번역하는 별도 부품을 사용한다.
    // {PAWN_labelShort}에는 현재 앵커의 공명자를 넣고, 공명자가 없으면 대체 문구를 사용한다.
    public class QuestPart_ViviEndingLetter : QuestPart
    {
        public string inSignal;
        public string labelKey;
        public string textKey;

        public string guideKey;
        public string guideDays;

        public LetterDef letterDef;
        public ArcanePlant_Everflower anchor;

        public override void Notify_QuestSignalReceived(Signal signal)
        {
            base.Notify_QuestSignalReceived(signal);
            if (signal.tag != inSignal) { return; }

            var text = ViviEndingUtility.TranslateWithResonator(textKey, ViviEndingUtility.ResonatorOf(anchor)).Resolve();
            if (!guideKey.NullOrEmpty())
            {
                text += "\n\n" + guideKey.Translate((guideDays ?? string.Empty).Named("DAYS")).Resolve();
            }

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
            Scribe_Values.Look(ref guideKey, "guideKey");
            Scribe_Values.Look(ref guideDays, "guideDays");
            Scribe_Defs.Look(ref letterDef, "letterDef");
            Scribe_References.Look(ref anchor, "anchor");
        }
    }
}
