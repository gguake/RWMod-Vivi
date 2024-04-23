using UnityEngine;
using Verse;

namespace VVRace
{
    public class ManaFluxNetworkNode : IExposable
    {
        public ManaAcceptor manaAcceptor;

        public float mana;

        public ManaFluxNetworkNode()
        {
        }

        public ManaFluxNetworkNode(ManaAcceptor manaAcceptor)
        {
            this.manaAcceptor = manaAcceptor;
        }

        public int LocalManaFluxForInspector
        {
            get
            {
                if (!manaAcceptor.HasManaFlux) { return 0; }

                var extension = manaAcceptor.ManaExtension;
                if (extension == null) { return 0; }

                var generatedMana = extension.manaGenerateRule?.CalcManaFlux(manaAcceptor) ?? 0;
                var consumedMana = extension.manaConsumeRule?.CalcManaFlux(manaAcceptor) ?? 0;
                return generatedMana - consumedMana;
            }
        }

        public void ExposeData()
        {
            Scribe_Values.Look(ref mana, "mana");

            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                mana = Mathf.Min(mana, manaAcceptor.def.GetModExtension<ManaExtension>().manaCapacity);
            }
        }
    }
}
