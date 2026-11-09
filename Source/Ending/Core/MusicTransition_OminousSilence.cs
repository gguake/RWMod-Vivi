using RimWorld;
using Verse;

namespace VVRace
{
    // 불길한 정적이 걸린 맵을 보고 있는 동안 배경음악을 끈다(무음 곡을 되풀이하는 시퀀스로 덮고, 정적이 풀리면 시퀀스가 끝난다).
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
