using RimWorld;
using Verse;

namespace VVRace
{
    public class MusicTransition_OminousSilence : MusicTransition
    {
        public override bool IsTransitionSatisfied()
        {
            if (!base.IsTransitionSatisfied()) { return false; }

            var conditionDef = VVGameConditionDefOf.VV_OminousSilence;
            var map = Find.CurrentMap;
            return conditionDef != null && map != null && map.gameConditionManager.ConditionIsActive(conditionDef);
        }
    }
}
