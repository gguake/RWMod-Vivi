using Verse;

namespace VVRace
{
    public class ArcanePlantExtension : DefModExtension
    {
        public int germinateWeight;
        public bool germinateRare;

        public int manaCapacity = 100;
        public float initialManaPercent = 1f;

        public ManaFluxRule manaConsumeRule;
        public ManaFluxRule manaGenerateRule;

        public int zeroManaDurableTicks = 2000;
        public IntRange zeroManaDamageByChance = new IntRange(0, 0);
        public int consumeManaPerVerbShoot = 0;

        public int terraformingTick = 0;
    }
}
