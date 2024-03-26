using RimWorld;
using Verse;

namespace VVRace
{
    internal static class SymbolResolverUtility
    {
        public static Thing MakeMinifiedArcanePlantRandom()
        {
            var def = ArcanePlant.AllArcanePlantDefs.RandomElement();
            var thing = ThingMaker.MakeThing(def).MakeMinified();
            return thing;
        }
    }
}
