using RimWorld;
using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace VVRace
{
    internal class ArtificialPlantDesignatorCache
    {
        private static Dictionary<ThingDef, Designator_ReplantArtificialPlant> _designatorInstallCache = new Dictionary<ThingDef, Designator_ReplantArtificialPlant>();

        public static Designator_ReplantArtificialPlant GetReplantDesignator(ThingDef def)
        {
            if (_designatorInstallCache.ContainsKey(def))
            {
                return _designatorInstallCache[def];
            }

            var designator = new Designator_ReplantArtificialPlant();
            designator.hotKey = KeyBindingDefOf.Misc1;
            _designatorInstallCache.Add(def, designator);
            return designator;
        }
    }
}
