using Verse;

namespace VVRace
{
    public class EnergyFluxNetworkNode : IExposable
    {
        public EnergyAcceptor energyAcceptor;

        public ArtificialPlant Plant => energyAcceptor as ArtificialPlant;

        public float energy;

        public int LocalEnergyFluxForInspector
        {
            get
            {
                var plant = Plant;
                if (plant == null) { return 0; }

                var extension = plant.ArtificialPlantModExtension;
                var generatedEnergy = extension.energyGenerateRule?.CalcEnergy(plant, 60000) ?? 0;
                var consumedEnergy = extension.energyConsumeRule?.CalcEnergy(plant, 60000) ?? 0;
                return (int)generatedEnergy - (int)consumedEnergy;
            }
        }

        public void ExposeData()
        {
            Scribe_Values.Look(ref energy, "energy");
        }
    }
}
