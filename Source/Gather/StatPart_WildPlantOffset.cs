using RimWorld;
using Verse;

namespace VVRace
{
    public class StatPart_WildPlantOffset : StatPart
    {
        public float offset;

        public override void TransformValue(StatRequest req, ref float val)
        {
            if (req.Thing is Plant plant && !plant.sown)
            {
                val += offset;
            }
        }

        public override string ExplanationPart(StatRequest req)
        {
            return null;
        }
    }
}
