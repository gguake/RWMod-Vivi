using RimWorld;
using Verse;
using Verse.AI;

namespace VVRace
{
    public class SensorWorker_Fire : SensorWorker
    {
        public override bool Detected(Thing parent, float radius)
        {
            var cells = GenRadial.NumCellsInRadius(radius);
            for (int i = 0; i < cells; ++i)
            {
                var map = parent.Map;
                var cell = parent.Position + GenRadial.RadialPattern[i];
                if (cell.InBounds(map) && cell.ContainsStaticFire(map) && GenSight.LineOfSight(parent.Position, cell, map))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
