using RimWorld;
using System.Collections.Generic;
using Verse;

namespace VVRace
{
    public class RitualObligationTargetFilterDef_EverflowerRitual : RitualObligationTargetFilterDef
    {
        public int requiredAttunementLevel;
    }

    public class RitualObligationTargetWorker_EverflowerRitual : RitualObligationTargetFilter
    {
        public RitualObligationTargetFilterDef_EverflowerRitual Def => (RitualObligationTargetFilterDef_EverflowerRitual)def;

        public RitualObligationTargetWorker_EverflowerRitual() { }
        public RitualObligationTargetWorker_EverflowerRitual(RitualObligationTargetFilterDef def) : base(def) { }

        public override IEnumerable<string> GetTargetInfos(RitualObligation obligation)
        {
            yield return LocalizeString_Etc.VV_RitualTarget_Everflower.Translate();
            yield break;
        }

        public override IEnumerable<TargetInfo> GetTargets(RitualObligation obligation, Map map)
        {
            foreach (var everflower in map.listerThings.ThingsOfDef(VVThingDefOf.VV_Everflower))
            {
                if (everflower.TryGetComp<CompEverflower>(out var compEverflower) && compEverflower.AttunementLevel >= Def.requiredAttunementLevel)
                {
                    yield return everflower;
                }
            }
        }

        protected override RitualTargetUseReport CanUseTargetInternal(TargetInfo target, RitualObligation obligation)
        {
            if (!target.HasThing) { return false; }

            if (!target.Thing.TryGetComp<CompEverflower>(out var compEverflower))
            {
                return false;
            }

            if (compEverflower.AttunementLevel < Def.requiredAttunementLevel)
            {
                return false;
            }

            return true;
        }
    }
}
