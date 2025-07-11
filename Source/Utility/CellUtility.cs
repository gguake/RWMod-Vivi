using Verse;

namespace VVRace.Utility
{
    internal static class CellUtility
    {
        public static IntVec3 FindLOSLastCell(Map map, IntVec3 from, IntVec3 target, float maxRange)
        {
            var lastLOSCell = GenSight.LastPointOnLineOfSight(
                from,
                from + ((target - from).ToVector3().normalized * maxRange).ToIntVec3(),
                validator: (cell) => cell.CanBeSeenOverFast(map),
                skipFirstCell: true);

            return lastLOSCell;
        }
    }
}
