using System;
using System.Text;
using Verse;

namespace VVRace
{
    public enum DreamumProjectStage
    {
        NotStarted,
        InProgress,
        Completed,
    }

    public class DreamumAltar : ManaAcceptor
    {
        private bool _projectStarted;
        private float _projectProgress;

        public override bool HasManaFlux => true;

        public override ManaFluxNetwork ManaFluxNetwork { get; set; }
        public override ManaFluxNetworkNode ManaFluxNode => _manaFluxNode;
        private ManaFluxNetworkNode _manaFluxNode;

        public DreamumAltar()
        {
            _manaFluxNode = new ManaFluxNetworkNode(this);
        }

        public override void ExposeData()
        {
            base.ExposeData();

            Scribe_Values.Look(ref _projectStarted, "projectStarted");
            Scribe_Values.Look(ref _projectProgress, "projectProgress");
        }

        public override void Tick()
        {
            base.Tick();

            if (_projectStarted)
            {

            }
        }

        public override string GetInspectString()
        {
            var sb = new StringBuilder(base.GetInspectString());
            if (sb.Length > 0)
            {
                sb.Append("\n");
            }

            sb.Append(LocalizeString_Inspector.VV_Inspector_PlantMana.Translate((int)_manaFluxNode.mana, ManaExtension.manaCapacity));

            if (Spawned)
            {
                var manaFlux = _manaFluxNode.LocalManaFluxForInspector;
                if (manaFlux != 0)
                {
                    sb.Append(" ");
                    sb.Append(LocalizeString_Inspector.VV_Inspector_PlantManaFlux.Translate(manaFlux.ToString("+0;-#")));
                }

                if (DebugSettings.godMode && ManaFluxNetwork != null)
                {
                    sb.AppendLine();
                    sb.Append($"flux network: {ManaFluxNetwork.NetworkHash}");
                }
            }

            return sb.ToString();
        }

        public override void PostPrint(SectionLayer layer)
        {
            if (!Spawned) { return; }

            foreach (var thing in AdjacentManaTransmitter)
            {
                PrintManaFluxWirePieceConnecting(layer, thing, false);
                return;
            }
        }

        public override void PrintForManaFluxGrid(SectionLayer layer)
        {
            PrintOverlayConnectorBaseFor(layer);

            foreach (var thing in AdjacentManaAcceptor)
            {
                PrintManaFluxWirePieceConnecting(layer, thing, true);
            }
        }

    }
}
