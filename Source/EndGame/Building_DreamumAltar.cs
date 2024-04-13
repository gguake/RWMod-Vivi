using RimWorld;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using Verse;

namespace VVRace
{
    public enum DreamumProjectStage
    {
        None,
        Prepare,
        InProgress,
        Completed,
    }

    [StaticConstructorOnStartup]
    public class Building_DreamumAltar : ManaAcceptor
    {
        public DreamumProjectStage Stage => _stage;
        private DreamumProjectStage _stage;

        public override bool HasManaFlux => true;

        public override ManaFluxNetwork ManaFluxNetwork { get; set; }
        public override ManaFluxNetworkNode ManaFluxNode => _manaFluxNode;
        private ManaFluxNetworkNode _manaFluxNode;

        public int _progressTicks;

        public Building_DreamumAltar()
        {
            _manaFluxNode = new ManaFluxNetworkNode(this);
        }

        public override void ExposeData()
        {
            base.ExposeData();

            Scribe_Values.Look(ref _stage, "stage");
            Scribe_Deep.Look(ref _manaFluxNode, "manaFluxNode");

            Scribe_Values.Look(ref _progressTicks, "progressTicks");
        }

        public override void Tick()
        {
            base.Tick();

            switch (_stage)
            {
                case DreamumProjectStage.Prepare:
                    if (_manaFluxNode.mana >= ManaExtension.manaCapacity - 1)
                    {
                        _stage = DreamumProjectStage.InProgress;
                    }
                    break;

                case DreamumProjectStage.InProgress:
                    {
                        if (_manaFluxNode.mana >= 1f)
                        {
                            _progressTicks++;
                        }
                    }
                    break;

                case DreamumProjectStage.Completed:
                    {
                        if (_manaFluxNode.mana >= 1f)
                        {
                            _progressTicks++;
                        }
                    }
                    break;
            }
        }

        public override void Destroy(DestroyMode mode = DestroyMode.Vanish)
        {
            base.Destroy(mode);
            QuestUtility.SendQuestTargetSignals(Map.Parent.questTags, "AltarDestroyed");
        }

        private static readonly Texture2D StartCommandTex = ContentFinder<Texture2D>.Get("UI/Commands/VV_Dreamum");
        public override IEnumerable<Gizmo> GetGizmos()
        {
            foreach (var gizmo in base.GetGizmos())
            {
                yield return gizmo;
            }

            if (_stage == DreamumProjectStage.None)
            {
                var commandStart = new Command_Action();
                commandStart.defaultLabel = LocalizeString_Command.VV_Command_StartProgressDreamumVictory.Translate();
                commandStart.defaultDesc = LocalizeString_Command.VV_Command_StartProgressDreamumVictoryDesc.Translate();
                commandStart.hotKey = KeyBindingDefOf.Misc1;
                commandStart.icon = StartCommandTex;
                commandStart.action = () =>
                {
                    var diaNode = new DiaNode(LocalizeString_Dialog.VV_DialogStartProgressDreamumVictoryCaution.Translate());
                    diaNode.options.Add(new DiaOption("Confirm".Translate())
                    {
                        action = () =>
                        {
                            _stage = DreamumProjectStage.Prepare;
                        },
                        resolveTree = true,
                    });
                    diaNode.options.Add(new DiaOption("GoBack".Translate())
                    {
                        resolveTree = true,
                    });

                    Find.WindowStack.Add(new Dialog_NodeTree(diaNode, delayInteractivity: true));
                };

                yield return commandStart;
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
