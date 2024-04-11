using Verse;

namespace VVRace
{
    public class ManaFluxRule_DreamumAltar : ManaFluxRule
    {
        public float mana;

        public override IntRange ApproximateManaFlux => new IntRange((int)mana, (int)mana);

        public override float CalcManaFlux(ManaAcceptor manaAcceptor, int ticks)
        {
            if (manaAcceptor is Building_DreamumAltar altar && altar.Stage == DreamumProjectStage.InProgress)
            {
                return mana / 60000f * ticks;
            }

            return 0f;
        }
    }
}
