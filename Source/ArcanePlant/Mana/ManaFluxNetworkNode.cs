using Verse;

namespace VVRace
{
    public class ManaFluxNetworkNode : IExposable
    {
        public ManaAcceptor manaAcceptor;

        public ArcanePlant Plant => manaAcceptor as ArcanePlant;

        public float mana;

        public int LocalManaFluxForInspector
        {
            get
            {
                var plant = Plant;
                if (plant == null) { return 0; }

                var extension = plant.ArcanePlantModExtension;
                var generatedMana = extension.manaGenerateRule?.CalcManaFlux(plant, 60000) ?? 0;
                var consumedMana = extension.manaConsumeRule?.CalcManaFlux(plant, 60000) ?? 0;
                return (int)generatedMana - (int)consumedMana;
            }
        }

        public void ExposeData()
        {
            Scribe_Values.Look(ref mana, "mana");
        }
    }
}
