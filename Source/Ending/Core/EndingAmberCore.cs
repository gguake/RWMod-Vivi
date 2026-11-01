using RimWorld;
using Verse;

namespace VVRace
{
    public partial class ViviEndingQuest
    {
        // 밀서를 재생성해도 의뢰가 유지되도록 마지막 코어 퀘스트를 밀서 부품 대신 이 컨트롤러에 보관한다.
        private Quest _amberCoreQuest;

        public Quest AmberCoreQuest => _amberCoreQuest;

        public bool IsAmberCoreQuestActive => _amberCoreQuest != null
            && (_amberCoreQuest.State == QuestState.NotYetAccepted || _amberCoreQuest.State == QuestState.Ongoing);

        public bool CanOfferAmberCoreRequest => Stage == ViviEndingStage.AltarUnlocked;

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

            var unmet = ViviEndingRequirements.GetUnmetReason();
            if (unmet != null)
            {
                return new AcceptanceReport(unmet);
            }

            return true;
        }

        // 호출자가 CanRequestAmberCoreQuest로 의뢰 가능 여부를 확인해야 한다. 개발자 도구에서는 이 검사를 생략한다.
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
