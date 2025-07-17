using RimWorld;
using System.Linq;
using Verse;

namespace VVRace
{
    internal static class SymbolResolverUtility
    {
        public static Thing MakeMinifiedArcanePlantRandom()
        {
            var def = ArcanePlant.AllGeneratableArcanePlantDefs.RandomElement();
            var thing = ThingMaker.MakeThing(def).MakeMinified();
            return thing;
        }
    }
}
