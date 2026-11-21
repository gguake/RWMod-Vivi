using System.Collections.Generic;
using Verse;

namespace VVRace
{
    public class CompProperties_Hornet : CompProperties
    {
        public float killedLeavingsChance = 1f;
        public List<ThingDefCountRangeClass> killedLeavingsRanges;

        public CompProperties_Hornet()
        {
            compClass = typeof(CompHornet);
        }
    }
}
