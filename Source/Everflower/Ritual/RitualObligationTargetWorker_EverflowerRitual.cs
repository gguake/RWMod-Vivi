using RimWorld;
using System.Collections.Generic;
using Verse;

namespace VVRace
{
    public class RitualObligationTargetFilterDef_EverflowerRitual : RitualObligationTargetFilterDef
    {
        public int requiredAttunementLevel;

        public int maxAttunementLevel = 99;

        public bool requiresAltar;

        public bool startsEndingQuest;

        public bool completesEndingQuest;
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
                if (everflower.TryGetComp<CompEverflower>(out var compEverflower) &&
                    compEverflower.AttunementLevel >= Def.requiredAttunementLevel &&
                    compEverflower.AttunementLevel <= Def.maxAttunementLevel)
                {
                    yield return everflower;
                }
            }
        }

        // false는 기즈모를 숨기고, 사유 문자열은 기즈모를 비활성화한 채 해당 사유를 표시한다.
        protected override RitualTargetUseReport CanUseTargetInternal(TargetInfo target, RitualObligation obligation)
        {
            if (!target.HasThing) { return false; }

            if (!target.Thing.TryGetComp<CompEverflower>(out var compEverflower))
            {
                return false;
            }

            if (compEverflower.AttunementLevel < Def.requiredAttunementLevel || compEverflower.AttunementLevel > Def.maxAttunementLevel)
            {
                return false;
            }

            if (!Def.startsEndingQuest && !Def.completesEndingQuest
                && EverflowerRitualRules.IsBlockedByEnding(compEverflower.Everflower)
                && !EverflowerRitualRules.IsAttunementAllowedDuringEnding(parent))
            {
                return LocalizeString_Etc.VV_FailReason_EndingInProgress.Translate();
            }

            if (Def.startsEndingQuest)
            {
                var reason = EverflowerRitualRules.GetEndingStartBlockReason();
                if (reason != null)
                {
                    return reason;
                }
            }

            if (Def.completesEndingQuest)
            {
                var reason = EverflowerRitualRules.GetCoronationBlockReason(compEverflower.Everflower);
                if (reason != null)
                {
                    return reason;
                }
            }

            if (Def.requiresAltar)
            {
                if (!compEverflower.IsEnshrined)
                {
                    return LocalizeString_Etc.VV_FailReason_RequiresEverflowerAltar.Translate();
                }

                var location = compEverflower.EnshrinedAltar?.LocationReport ?? true;
                if (!location.Accepted)
                {
                    return location.Reason;
                }
            }

            return true;
        }
    }
}
