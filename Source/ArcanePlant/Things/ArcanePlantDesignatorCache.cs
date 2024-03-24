using RimWorld;
using System.Collections.Generic;
using Verse;

namespace VVRace
{
    internal class ArcanePlantDesignatorCache
    {
        private static Dictionary<ThingDef, Designator_ReplantArcanePlant> _designatorInstallCache = new Dictionary<ThingDef, Designator_ReplantArcanePlant>();

        public static Designator_ReplantArcanePlant GetReplantDesignator(ThingDef def)
        {
            if (_designatorInstallCache.ContainsKey(def))
            {
                return _designatorInstallCache[def];
            }

            var designator = new Designator_ReplantArcanePlant();
            designator.hotKey = KeyBindingDefOf.Misc1;
            _designatorInstallCache.Add(def, designator);
            return designator;
        }
    }
}
