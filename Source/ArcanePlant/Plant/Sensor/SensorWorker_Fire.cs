using RimWorld;
using Verse;
using Verse.AI;

namespace VVRace
{
    public class SensorWorker_Fire : SensorWorker
    {
        public override string TargetLabel => LocalizeString_PlantFunction.VV_PlantFunction_SensorTargetFire.Translate();

        public override bool Detected(Thing parent, float radius)
        {
            var map = parent.Map;
            var origin = parent.Position;
            var cells = GenRadial.NumCellsInRadius(radius);
            for (int i = 0; i < cells; ++i)
            {
                var cell = origin + GenRadial.RadialPattern[i];
                if (cell.InBounds(map) && cell.ContainsStaticFire(map) && GenSight.LineOfSight(origin, cell, map))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
