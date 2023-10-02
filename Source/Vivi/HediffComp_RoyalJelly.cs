using Verse;

namespace VVRace
{
    public class HediffCompProperties_RoyalJelly : HediffCompProperties
    {
        public HediffCompProperties_RoyalJelly()
        {
            compClass = typeof(HediffComp_RoyalJelly);
        }
    }

    public class HediffComp_RoyalJelly : HediffComp
    {
        public override bool CompShouldRemove
        {
            get
            {
                var pawn = Pawn;
                if (pawn is Vivi vivi)
                {
                    if (vivi.IsRoyal || !vivi.DevelopmentalStage.Adult())
                    {
                        return false;
                    }
                }

                return true;
            }
        }
    }
}
