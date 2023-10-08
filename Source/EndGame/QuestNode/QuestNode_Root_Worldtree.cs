using RimWorld;
using RimWorld.Planet;
using RimWorld.QuestGen;
using Verse;

namespace VVRace
{
    public enum WorldtreeQuestStage
    {
        AssaultAncientComplex,
        AssaultMechanoidOutpost,
        RestoreWorldtree,
    }

    public class QuestNode_Root_Worldtree : QuestNode
    {
        protected override void RunInt()
        {
            var quest = QuestGen.quest;
            var slate = QuestGen.slate;

            var map = QuestGen_Get.GetMap();
            var points = slate.Get("points", 0f);
            Log.Message($"QuestNode_Root_Worldtree: {points}");

            quest.AddPart(new QuestPart_SubquestGenerator_Worldtree
            {
                inSignalEnable = slate.Get<string>("inSignal"),
                interval = new IntRange(0, 0),
                maxSuccessfulSubquests = 3,
                maxActiveSubquests = 1,
                useMapParentThreatPoints = map?.Parent,
                subquestDefs =
                {
                    VVQuestScriptDefOf.VV_EndGame_Worldtree_Step_Archaeologist,
                    VVQuestScriptDefOf.VV_EndGame_Worldtree_Step_AssaultComplex,
                    VVQuestScriptDefOf.VV_EndGame_Worldtree_Step_RestoreWorldtree
                }
            });
        }

        protected override bool TestRunInt(Slate slate)
        {
            return true;
        }
    }
}
