using RimWorld;
using Verse;

namespace VVRace
{
    // 퀘스트 설명란에 기록을 덧붙인다. 끝난 퀘스트는 30일 뒤 부품이 지워지므로 설명 문자열 자체에 남긴다.
    public class QuestPart_ViviEndingRecord : QuestPart
    {
        public string inSignal;
        public string textKey;

        private bool _recorded;

        public override void Notify_QuestSignalReceived(Signal signal)
        {
            base.Notify_QuestSignalReceived(signal);
            if (_recorded || signal.tag != inSignal) { return; }

            _recorded = true;
            quest.description = new TaggedString(quest.description.RawText + "\n\n" + textKey.Translate().Resolve());
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref inSignal, "inSignal");
            Scribe_Values.Look(ref textKey, "textKey");
            Scribe_Values.Look(ref _recorded, "recorded", false);
        }
    }
}
