using RimWorld;
using System.Text;
using Verse;

namespace VVRace
{
    public class ManaTransmitter : ManaAcceptor
    {
        public override ManaFluxNetwork ManaFluxNetwork { get; set; }
        public override ManaFluxNetworkNode ManaFluxNode => null;

        public override string GetInspectString()
        {
            var sb = new StringBuilder(base.GetInspectString());

            if (Spawned)
            {
                if (DebugSettings.godMode && ManaFluxNetwork != null)
                {
                    sb.AppendInNewLine($"flux network: {ManaFluxNetwork.NetworkHash}");
                }
            }

            return sb.ToString().TrimEndNewlines();
        }

        public override void PrintForManaFluxGrid(SectionLayer layer)
        {
            ManaAcceptorOverlayMats.LinkedOverlayGraphic.Print(layer, this, 0f);
        }
    }
}
