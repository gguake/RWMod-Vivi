using RimWorld;
using Verse;

namespace VVRace
{
    // QuestGen 안에서는 HardcodedSignalWithQuestID/HardcodedTargetQuestTagWithQuestID로, 밖에서는 For로 신호 이름을 만든다.
    public static class ViviEndingSignals
    {
        public const string GrandAttunementCompleted = "GrandAttunementCompleted";

        public const string BloomCondensed = "BloomCondensed";
        public const string CalmEnded = "CalmEnded";
        public const string ApocryphaManifesting = "ApocryphaManifesting";
        public const string ApocryphaArrived = "ApocryphaArrived";
        public const string ApocryphaDissolved = "ApocryphaDissolved";
        public const string ApocryphaDefeated = "ApocryphaDefeated";
        public const string CoronationCompleted = "CoronationCompleted";
        public const string EndingFinished = "EndingFinished";
        public const string EverflowerStolen = "EverflowerStolen";
        public const string AnchorLost = "AnchorLost";

        public const string ResonatorFallen = "ResonatorFallen";

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
