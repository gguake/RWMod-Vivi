using Verse;

namespace VVRace
{
    public class ArcanePlantExtension : DefModExtension
    {
        public int germinateWeight;
        public bool germinateRare;

        public int energyCapacity = 100;
        public float initialEnergyPercent = 1f;

        public EnergyRule energyConsumeRule;
        public EnergyRule energyGenerateRule;

        public int zeroEnergyDurableTicks = 2000;
        public IntRange zeroEnergyDamageByChance = new IntRange(0, 0);

        public int verbShootEnergy = 0;

        public int terraformingTick = 0;
    }
}
