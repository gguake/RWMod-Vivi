using System.Collections.Generic;
using Verse;

namespace VVRace
{
    public class GerminatorModExtension : DefModExtension
    {
        public List<ThingDefCountClass> germinateIngredients;
        public int scheduleCooldown;

        public int germinateFailureCriticalWeight;
        public int germinateFailureCropsWeight;

    }

}
