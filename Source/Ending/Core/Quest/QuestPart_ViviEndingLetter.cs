using RimWorld;
using Verse;

namespace VVRace
{
    // 보내는 순간 번역하는 엔딩 편지(평화 기간·대관식 대기). 본문의 {PAWN_labelShort}은 그 순간 앵커의 공명자로 채우고,
    // 공명자가 없으면 대체 문구를 쓴다. 생성 시점에 텍스트를 고정하는 바닐라 QuestPart_Letter로는 이 동작을 낼 수 없다.
    public class QuestPart_ViviEndingLetter : QuestPart
    {
        public string inSignal;
        public string labelKey;
        public string textKey;

        // 본문 뒤에 붙는 안내 문단과 그 {DAYS} 값(없으면 null)
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
