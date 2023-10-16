using System.Collections.Generic;
using Verse;

namespace VVRace
{
    public class EnergyFluxNetworkNode : IExposable
    {
        public ArtificialPlant plant;
        public List<EnergyFluxNetworkNode> connectedNodes = new List<EnergyFluxNetworkNode>();

        public float energy;

        public int LocalEnergyFluxForInspector
        {
            get
            {
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
