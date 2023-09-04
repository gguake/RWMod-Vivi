using Verse;

namespace VVRace
{
    public class CompProperties_FoodDrainWhenMake : CompProperties
    {
        public float drainPerStackCount = 0f;

        public CompProperties_FoodDrainWhenMake()
        {
            compClass = typeof(CompFoodDrainWhenMake);
        }
    }

    public class CompFoodDrainWhenMake : ThingComp
    {
        public CompProperties_FoodDrainWhenMake Props => (CompProperties_FoodDrainWhenMake)props;
    }
}
