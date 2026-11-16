using RimWorld;
using Verse;

namespace VVRace
{
    public class CompProperties_UseEffectRuinEcho : CompProperties_UseEffect
    {
        public int index;

        public CompProperties_UseEffectRuinEcho()
        {
            compClass = typeof(CompUseEffect_RuinEcho);
        }
    }

    public class CompUseEffect_RuinEcho : CompUseEffect
    {
        private CompProperties_UseEffectRuinEcho Props => (CompProperties_UseEffectRuinEcho)props;

        public int Index => Props.index;

        public override AcceptanceReport CanBeUsedBy(Pawn p)
        {
            var ruins = ViviEndingQuest.Current?.Ruins;
            if (ruins == null) { return false; }

            return ruins.CanInvestigate(Props.index, p);
        }

        public override void DoEffect(Pawn usedBy)
        {
            base.DoEffect(usedBy);
            ViviEndingQuest.Current?.Ruins.Investigate(Props.index, usedBy, parent);
        }
    }
}
