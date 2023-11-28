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
                return Pawn.GetCompVivi() == null;
            }
        }
    }
}
