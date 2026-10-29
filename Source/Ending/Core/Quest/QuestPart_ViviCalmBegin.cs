using RimWorld;
using Verse;

namespace VVRace
{
    // 〈폭풍 전의 고요〉가 시작될 때 앵커 맵을 정리한다: 대기열의 위협 사건 삭제, 야생 거대 말벌 소멸, 공격 중인 습격대 철수.
    // 설계서: "모든 일반 습격이 즉시 중단", "거대 말벌 무리조차 그림자 속으로 자취를 감추었습니다".
    // 평화 기간 동안의 새 위협은 위협 억제 패치(EndingCorePatch)가 막는다.
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
