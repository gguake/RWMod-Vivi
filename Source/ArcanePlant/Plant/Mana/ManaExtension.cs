using Verse;

namespace VVRace
{
    public class ManaExtension : DefModExtension
    {
        public int manaCapacity = 100;
        public float initialManaPercent = 1f;

        public ManaFluxRule manaConsumeRule;
        public ManaFluxRule manaGenerateRule;
    }
}
