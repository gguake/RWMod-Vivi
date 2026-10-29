using RimWorld;
using Verse;

namespace VVRace
{
    // 엔딩 퀘스트 신호·퀘스트 태그 이름. 실제 신호는 바닐라 형식인 "Quest{id}.<이름>"이다.
    // 퀘스트 생성 중(QuestGen)에는 QuestGenUtility.HardcodedSignalWithQuestID/HardcodedTargetQuestTagWithQuestID로, 그 밖에서는 For로 만든다.
    public static class ViviEndingSignals
    {
        // 〈비비 연방의 밀서〉: 5단계 대조율 의식 완료(창구가 보낸다) → 밀서 퀘스트 성공
        public const string GrandAttunementCompleted = "GrandAttunementCompleted";

        // 〈만개하는 여명〉 단계 신호
        public const string BloomCondensed = "BloomCondensed";
        public const string CalmEnded = "CalmEnded";
        public const string ApocryphaArrived = "ApocryphaArrived";
        public const string ApocryphaDissolved = "ApocryphaDissolved";
        public const string ApocryphaDefeated = "ApocryphaDefeated";
        public const string CoronationCompleted = "CoronationCompleted";
        public const string EndingFinished = "EndingFinished";
        public const string EverflowerStolen = "EverflowerStolen";
        public const string AnchorLost = "AnchorLost";

        // 퀘스트 태그: 대상에 붙여 두면 바닐라가 "<태그>.Destroyed", "<태그>.MapRemoved" 등을 보낸다.
        public const string AnchorTag = "anchor";
        public const string AnchorMapTag = "anchorMap";

        public static string For(Quest quest, string name)
        {
            return $"Quest{quest.id}.{name}";
        }

        public static void Send(Quest quest, string name, params NamedArgument[] args)
        {
            if (quest == null) { return; }

            Find.SignalManager.SendSignal(new Signal(For(quest, name), args));
        }
    }
}
