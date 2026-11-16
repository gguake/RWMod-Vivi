using RimWorld;
using Verse;

namespace VVRace
{
    public class CompUseEffect_ExtractAmberCore : CompUseEffect
    {
        public override AcceptanceReport CanBeUsedBy(Pawn p)
        {
            var ruins = ViviEndingQuest.Current?.Ruins;
            if (ruins == null) { return false; }

            return ruins.CanExtractCore(p);
        }

        public override void DoEffect(Pawn usedBy)
        {
            base.DoEffect(usedBy);
            ViviEndingQuest.Current?.Ruins.ExtractCore(usedBy, parent);
        }
    }
}
