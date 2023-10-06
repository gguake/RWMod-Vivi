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
                var compVivi = pawn.GetCompVivi();
                if (compVivi != null && (compVivi.isRoyal || !pawn.DevelopmentalStage.Adult()))
                {
                    return false;
                }

                return true;
            }
        }
    }
}
