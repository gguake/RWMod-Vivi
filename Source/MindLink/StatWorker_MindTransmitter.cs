using RimWorld;
using Verse;

namespace VVRace
{
    public class StatWorker_MindTransmitter : StatWorker
    {
        public override bool ShouldShowFor(StatRequest req)
        {
            if (!base.ShouldShowFor(req))
            {
                return false;
            }

            if (req.Thing != null && req.Thing is Pawn pawn)
            {
                return pawn.HasMindTransmitter();
            }

            return false;
        }
    }
}
