using Verse;

namespace VVRace
{
    public class ManaFluxRule_DreamumAltar : ManaFluxRule
    {
        public int mana;

        public override IntRange ApproximateManaFlux => new IntRange(mana, mana);

        public override string GetRuleString(bool inverse) => "";

        public override int CalcManaFlux(ManaAcceptor manaAcceptor)
        {
            if (manaAcceptor is Building_DreamumAltar altar && altar.Stage == DreamumProjectStage.InProgress)
            {
                return mana;
            }

            return 0;
        }
    }
}
