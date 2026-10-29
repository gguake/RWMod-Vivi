using RimWorld;
using Verse;

namespace VVRace
{
    // 고대 호박 코어 의뢰: 밀서 퀘스트가 진행 중인 동안 통신기로 비비 연방에 3성 무작위 퀘스트를 의뢰하고(AmberCoreCommsOption),
    // 보상으로 코어를 받는다(ViviAmberCoreQuestGen). 코어는 게임에 하나만 있을 수 있어,
    // 의뢰한 퀘스트가 끝나지 않았거나 코어(또는 제단)가 남아 있으면 다시 의뢰할 수 없다(AmberCoreUtility).
    public partial class ViviEndingQuest
    {
        // 마지막으로 의뢰한 고대 호박 코어 퀘스트(없으면 null). 밀서가 다시 만들어져도 이어지도록 밀서 부품이 아닌 창구에 둔다.
        private Quest _amberCoreQuest;

        public Quest AmberCoreQuest => _amberCoreQuest;

        // 의뢰한 퀘스트가 아직 끝나지 않았는지. 퀘스트 탭에서 숨기기만 한 수락 대기 퀘스트도 만료될 때까지 포함한다.
        public bool IsAmberCoreQuestActive => _amberCoreQuest != null
            && (_amberCoreQuest.State == QuestState.NotYetAccepted || _amberCoreQuest.State == QuestState.Ongoing);

        // 통신기 대화에 의뢰 선택지를 보일지: 밀서 퀘스트가 진행 중이고 엔딩이 시작되기 전.
        public bool CanOfferAmberCoreRequest => Stage == ViviEndingStage.AltarUnlocked;

        // 지금 연방에 의뢰할 수 있는지. 안 되면 비활성 사유.
        public AcceptanceReport CanRequestAmberCoreQuest(Faction federation)
        {
            if (federation == null || federation.HostileTo(Faction.OfPlayer))
            {
                return new AcceptanceReport(LocalizeString_EndingCore.VV_Comms_AmberCoreRequestHostile.Translate());
            }

            if (IsAmberCoreQuestActive)
            {
                return new AcceptanceReport(LocalizeString_EndingCore.VV_Comms_AmberCoreRequestActive.Translate());
            }

            if (AmberCoreUtility.AnyAmberCoreExists())
            {
                return new AcceptanceReport(LocalizeString_EndingCore.VV_Comms_AmberCoreRequestCoreExists.Translate());
            }

            return true;
        }

        // 의뢰 퀘스트를 만들어 퀘스트 목록에 넣고 바닐라 퀘스트 제안 편지를 보낸다. 맡길 퀘스트를 찾지 못하면 null.
        // 의뢰할 수 있는지(CanRequestAmberCoreQuest)는 부르는 쪽이 확인한다(DEV 도구는 건너뛴다).
        public Quest RequestAmberCoreQuest(Faction federation)
        {
            var quest = ViviAmberCoreQuestGen.TryGenerate(federation);
            if (quest == null) { return null; }

            _amberCoreQuest = quest;
            Find.QuestManager.Add(quest);
            if (!quest.hidden && quest.root.sendAvailableLetter)
            {
                QuestUtility.SendLetterQuestAvailable(quest);
            }

            return quest;
        }
    }
}
