using RimWorld;
using RimWorld.Planet;
using RimWorld.QuestGen;
using System.Linq;
using System.Reflection;
using Verse;

namespace VVRace
{
    public class QuestPart_SubquestGenerator_Worldtree : QuestPart_SubquestGenerator
    {
        public WorldtreeQuestStage stage;
        public MapParent useMapParentThreatPoints;

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref stage, "stage");
            Scribe_References.Look(ref useMapParentThreatPoints, "useMapParentThreatPoints");
        }

        protected override Slate InitSlate()
        {
            float threatPoints = 0f;
            if (useMapParentThreatPoints != null)
            {
                if (useMapParentThreatPoints.HasMap)
                {
                    threatPoints = StorytellerUtility.DefaultThreatPointsNow(useMapParentThreatPoints.Map);
                }
                else if (Find.AnyPlayerHomeMap == null)
                {
                    threatPoints = StorytellerUtility.DefaultThreatPointsNow(Find.World);
                }
                else
                {
                    threatPoints = StorytellerUtility.DefaultThreatPointsNow(Find.AnyPlayerHomeMap);
                }
            }

            var slate = new Slate();
            slate.Set("points", threatPoints);
            return slate;
        }

        protected override QuestScriptDef GetNextSubquestDef()
        {
            int index = quest.GetSubquests(QuestState.EndedSuccess).Count() % subquestDefs.Count;
            var questDef = subquestDefs[index];
            if (questDef.CanRun(InitSlate()))
            {
                return questDef;
            }

            return null;
        }
    }

}
