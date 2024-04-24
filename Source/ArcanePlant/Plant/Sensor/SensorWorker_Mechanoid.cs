using Verse;

namespace VVRace
{

    public class SensorWorker_Mechanoid : SensorWorker
    {
        public override bool Detected(Thing parent, float radius)
        {
            var cells = GenRadial.NumCellsInRadius(radius);
            for (int i = 0; i < cells; ++i)
            {
                var map = parent.Map;
                var cell = parent.Position + GenRadial.RadialPattern[i];
                if (cell.InBounds(map) && 
                    GenSight.LineOfSight(parent.Position, cell, map) &&
                    cell.GetThingList(map).Any(thing => thing is Pawn pawn && pawn.RaceProps?.IsMechanoid == true))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
