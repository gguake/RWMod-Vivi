using RimWorld;
using RimWorld.QuestGen;
using System.Linq;
using Verse;

namespace VVRace
{
    public class QuestNode_FixedFaction : QuestNode
    {
        [NoTranslate]
        public SlateRef<string> storeAs;

        public FactionDef factionDef;

        protected override bool TestRunInt(Slate slate)
        {
            if (slate.TryGet<Faction>(storeAs.GetValue(slate), out var var) && IsGoodFaction(var, slate))
            {
                return true;
            }

            if (TryFindFaction(out var, slate))
            {
                slate.Set(storeAs.GetValue(slate), var);
                return true;
            }
            return false;
        }

        protected override void RunInt()
        {
            Slate slate = QuestGen.slate;
            if ((!QuestGen.slate.TryGet<Faction>(storeAs.GetValue(slate), out var var) || !IsGoodFaction(var, QuestGen.slate)) && TryFindFaction(out var, QuestGen.slate))
            {
                QuestGen.slate.Set(storeAs.GetValue(slate), var);
                if (!var.Hidden)
                {
                    QuestPart_InvolvedFactions questPart_InvolvedFactions = new QuestPart_InvolvedFactions();
                    questPart_InvolvedFactions.factions.Add(var);
                    QuestGen.quest.AddPart(questPart_InvolvedFactions);
                }
            }
        }

        private bool TryFindFaction(out Faction faction, Slate slate)
        {
            return (from x in Find.FactionManager.GetFactions(allowHidden: true)
                    where IsGoodFaction(x, slate)
                    select x).TryRandomElement(out faction);
        }

        private bool IsGoodFaction(Faction faction, Slate slate)
        {
            return faction.def == FactionDefOf.Mechanoid;
        }
    }
}
