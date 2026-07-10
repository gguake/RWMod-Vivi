using RimWorld;

namespace VVRace
{
    public class Apparel_PerfumeBottle : Apparel
    {
        public override string LabelNoCount
        {
            get
            {
                var comp = GetComp<CompPerfumeBottle>();
                return comp?.DynamicLabel ?? base.LabelNoCount;
            }
        }
    }
}
