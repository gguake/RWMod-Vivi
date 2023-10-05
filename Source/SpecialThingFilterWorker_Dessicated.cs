using RimWorld;
using Verse;

namespace VVRace
{
    public class SpecialThingFilterWorker_Dessicated : SpecialThingFilterWorker
    {
        public override bool Matches(Thing t)
        {
            CompRottable compRottable = t.TryGetComp<CompRottable>();
            if (compRottable == null || compRottable.PropsRot.rotDestroys)
            {
                return false;
            }
            return compRottable.Stage == RotStage.Dessicated;
        }

        public override bool CanEverMatch(ThingDef def)
        {
            CompProperties_Rottable compProperties = def.GetCompProperties<CompProperties_Rottable>();
            if (compProperties != null)
            {
                return !compProperties.rotDestroys;
            }
            return false;
        }
    }
}
