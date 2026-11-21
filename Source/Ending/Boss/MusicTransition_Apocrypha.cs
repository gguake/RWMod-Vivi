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
            return comp != null && comp.parent.MapHeld != null && comp.IsQuestBossFight && PlaysInPhase(comp.Phase);
        }
    }

    public class MusicTransition_ApocryphaQueen : MusicTransition_Apocrypha
    {
        protected override bool PlaysInPhase(ApocryphaPhase phase) => phase == ApocryphaPhase.Queen;
    }

    public class MusicTransition_ApocryphaAsphodel : MusicTransition_Apocrypha
    {
        protected override bool PlaysInPhase(ApocryphaPhase phase) => phase == ApocryphaPhase.Rebirth || phase == ApocryphaPhase.Asphodel;
    }
}
