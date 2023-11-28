using Verse;

namespace VVRace
{
    public class HediffCompProperties_GrowthBoost : HediffCompProperties
    {
        public HediffCompProperties_GrowthBoost()
        {
            compClass = typeof(HediffComp_GrowthBoost);
        }
    }

    public class HediffComp_GrowthBoost : HediffComp
    {
        public override bool CompShouldRemove
        {
            get
            {
                return Pawn.GetCompVivi() == null || Pawn.DevelopmentalStage.Adult();
            }
        }
    }
}
