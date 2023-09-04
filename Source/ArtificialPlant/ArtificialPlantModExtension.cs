using Verse;

namespace VVRace
{
    public class ArtificialPlantModExtension : DefModExtension
    {
        public int energyCapacity = 100;
        public float initialEnergyPercent = 1f;

        public EnergyRule energyConsumeRule;
        public EnergyRule energyGenerateRule;

        public int zeroEnergyDurableTicks = 2000;
        public IntRange zeroEnergyDamageByChance = new IntRange(0, 0);

        public int fireExtinguishMinimumEnergy = 250;
        public int fireExtinguishEnergy = 200;
        public float fireExtinguishSensorRadius = 0f;
        public float fireExtinguishExplosiveRadius = 0f;

        public int empExplosiveMinimumEnergy = -1;
        public int empExplosiveEnergy = 0;
        public FloatRange empExplosiveRadiusRange = new FloatRange(0f, 0f);
    }
}
