using RPEF;
using Verse;

namespace VVRace
{
    public class PawnRenderSubWorker_WingCondition : PawnRenderSubWorker_DrawConditionDevelopmentStage
    {
        public override DevelopmentalStage DevelopmentalStage => DevelopmentalStage.Child | DevelopmentalStage.Adult;
    }
}
