using Verse;
using Verse.AI.Group;

namespace VVRace
{
    public abstract class LordToil_ViviNest : LordToil
    {
        public override void Notify_BuildingSpawnedOnMap(Building b)
        {
            base.Notify_BuildingSpawnedOnMap(b);
            (lord?.LordJob as LordJob_ViviNest)?.Notify_BuildingChanged(b);
        }

        public override void Notify_BuildingDespawnedOnMap(Building b)
        {
            base.Notify_BuildingDespawnedOnMap(b);
            (lord?.LordJob as LordJob_ViviNest)?.Notify_BuildingChanged(b);
        }
    }
}
