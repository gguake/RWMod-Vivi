using Verse;

namespace VVRace
{
    public class HediffComp_EverflowerLink : HediffComp
    {
        public override string CompLabelInBracketsExtra
        {
            get
            {
                var str = (parent.Severity - (int)parent.Severity).ToStringPercent();
                Log.Message($"{str}");
                return str;
            }
        }
    }
}
