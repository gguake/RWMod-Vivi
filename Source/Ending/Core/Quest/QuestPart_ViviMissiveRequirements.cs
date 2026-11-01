using RimWorld;

namespace VVRace
{
    public class QuestPart_ViviMissiveRequirements : QuestPart
    {
        public override string DescriptionPart => quest.State == QuestState.Ongoing ? ViviEndingRequirements.ProgressText() : null;
    }
}
