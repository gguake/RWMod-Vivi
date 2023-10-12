using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verse;

namespace VVRace
{
    internal static class SymbolResolverUtility
    {
        public static Thing MakeMinifiedArtificialPlantRandom()
        {
            var def = ArtificialPlant.AllArtificialPlantDefs.RandomElement();
            var thing = ThingMaker.MakeThing(def).MakeMinified();
            return thing;
        }
    }
}
