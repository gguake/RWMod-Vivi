using RimWorld;
using System.Text;
using Verse;

namespace VVRace
{
    public class EnergyTransmitter : EnergyAcceptor
    {
        public override EnergyFluxNetwork EnergyFluxNetwork { get; set; }
        public override EnergyFluxNetworkNode EnergyFluxNode => null;

        public override string GetInspectString()
        {
            var sb = new StringBuilder(base.GetInspectString());

            if (Spawned)
            {
                if (DebugSettings.godMode && EnergyFluxNetwork != null)
                {
                    sb.AppendInNewLine($"flux network: {EnergyFluxNetwork.NetworkHash}");
                }
            }

            return sb.ToString().TrimEndNewlines();
        }

        public override void PrintForEnergyGrid(SectionLayer layer)
        {
            EnergyAcceptorOverlayMats.LinkedOverlayGraphic.Print(layer, this, 0f);
        }
    }
}
