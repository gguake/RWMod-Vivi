using RimWorld;
using UnityEngine;
using Verse;

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

    public class MusicTransition_ApocryphaRebirth : MusicTransition_Apocrypha
    {
        protected override bool PlaysInPhase(ApocryphaPhase phase) => phase == ApocryphaPhase.Rebirth;
    }

    public class MusicTransition_ApocryphaAsphodel : MusicTransition_Apocrypha
    {
        protected override bool PlaysInPhase(ApocryphaPhase phase) => phase == ApocryphaPhase.Asphodel;
    }

    public class MusicTransition_ApocryphaDefeat : MusicTransition
    {
        private const int GraceTicks = 1800;

        private int _graceUntilTick = -1;

        public override bool IsTransitionSatisfied()
        {
            if (!base.IsTransitionSatisfied()) { return false; }

            var current = musicManager.TriggeredTransition;
            if (current != this && !(current is MusicTransition_Apocrypha)) { return false; }

            var now = GenTicks.TicksGame;
            if (AnyDefeatSequence())
            {
                _graceUntilTick = current == this ? Mathf.Max(_graceUntilTick, now + GraceTicks) : now + DyingApocrypha.DissolveTicks + GraceTicks;
                return true;
            }

            return current == this && now < _graceUntilTick;
        }

        private static bool AnyDefeatSequence()
        {
            var maps = Find.Maps;
            for (int i = 0; i < maps.Count; i++)
            {
                if (maps[i].listerThings.ThingsOfDef(VVThingDefOf.VV_DyingApocrypha).Count > 0) { return true; }
            }

            return false;
        }
    }
}
