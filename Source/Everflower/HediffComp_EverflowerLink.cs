using Verse;

namespace VVRace
{
    public class HediffComp_EverflowerLink : HediffComp
    {
        public override bool CompShouldRemove
        {
            get
            {
                var compVivi = Pawn.GetCompVivi();
                if (compVivi == null || !compVivi.AttunementActive) { return true; }

                return false;
            }
        }

        public override string CompLabelInBracketsExtra
        {
            get
            {
                var str = (parent.Severity - (int)parent.Severity).ToStringPercent();
                return str;
            }
        }
    }
}
