using RimWorld;

namespace VVRace
{
    public abstract class MusicTransition_Apocrypha : MusicTransition
    {
        protected abstract bool PlaysInPhase(ApocryphaPhase phase);

        public override bool IsTransitionSatisfied()
        {
            if (!base.IsTransitionSatisfied()) { return false; }

            var comp = ViviEndingQuest.Current?.BossComp;
            return comp != null && comp.parent.Spawned && comp.IsQuestBossFight && PlaysInPhase(comp.Phase);
        }
    }

    public class MusicTransition_ApocryphaBound : MusicTransition_Apocrypha
    {
        protected override bool PlaysInPhase(ApocryphaPhase phase) => phase == ApocryphaPhase.Bound;
    }

    public class MusicTransition_ApocryphaUnbound : MusicTransition_Apocrypha
    {
        protected override bool PlaysInPhase(ApocryphaPhase phase) => phase == ApocryphaPhase.Transforming || phase == ApocryphaPhase.Unbound;
    }
}
