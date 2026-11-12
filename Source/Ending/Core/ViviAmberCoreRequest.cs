using RimWorld;
using Verse;

namespace VVRace
{
    public class ViviAmberCoreRequest : IExposable
    {
        private readonly ViviEndingQuest _ending;

        private Quest _quest;

        public Quest Quest => _quest;

        public bool IsQuestActive => _quest != null
            && (_quest.State == QuestState.NotYetAccepted || _quest.State == QuestState.Ongoing);

        public bool CanOffer => _ending.Stage == ViviEndingStage.AltarUnlocked;

        public ViviAmberCoreRequest(ViviEndingQuest ending)
        {
            _ending = ending;
        }

        public void ExposeData()
        {
            Scribe_References.Look(ref _quest, "quest");
        }

        public AcceptanceReport CanRequest(Faction federation)
        {
            if (federation == null || federation.HostileTo(Faction.OfPlayer))
            {
                return new AcceptanceReport(LocalizeString_Dialog.VV_Comms_AmberCoreRequestHostile.Translate());
            }

            if (IsQuestActive)
            {
                return new AcceptanceReport(LocalizeString_Dialog.VV_Comms_AmberCoreRequestActive.Translate());
            }

            if (AmberCoreUtility.AnyAmberCoreExists())
            {
                return new AcceptanceReport(LocalizeString_Dialog.VV_Comms_AmberCoreRequestCoreExists.Translate());
            }

            var unmet = ViviEndingRequirements.GetUnmetReason();
            if (unmet != null)
            {
                return new AcceptanceReport(unmet);
            }

            return true;
        }

        public Quest Request(Faction federation)
        {
            var quest = ViviAmberCoreQuestGen.TryGenerate(federation);
            if (quest == null) { return null; }

            _quest = quest;
            Find.QuestManager.Add(quest);
            if (!quest.hidden && quest.root.sendAvailableLetter)
            {
                QuestUtility.SendLetterQuestAvailable(quest);
            }

            return quest;
        }
    }
}
