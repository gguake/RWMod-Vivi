using RimWorld;
using Verse;

namespace VVRace
{
    public class SensorWorker_Fire : SensorWorker
    {
        public override bool Detected(Thing parent, float radius)
        {
            var cells = GenRadial.NumCellsInRadius(radius);
            for (int i = 0; i < cells; ++i)
            {
                var cell = parent.Position + GenRadial.RadialPattern[i];
                if (!cell.InBounds(parent.Map)) { continue; }

                var things = cell.GetThingList(parent.Map);
                foreach (var thing in things)
                {
                    if (thing is Fire || thing.HasAttachment(ThingDefOf.Fire))
                    {
                        return true;
                    }
                }
            }

            return false;
        }
    }
}
