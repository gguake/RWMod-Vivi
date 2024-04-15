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
    public class Building_DreamumAltar : ManaAcceptor, IConditionalGraphicProvider, IThingHolder
    {
        public DreamumProjectStage Stage => _stage;
        private DreamumProjectStage _stage;

        public override bool HasManaFlux => true;

        public override ManaFluxNetwork ManaFluxNetwork { get; set; }
        public override ManaFluxNetworkNode ManaFluxNode => _manaFluxNode;

        public bool RequireDreamum => Stage >= DreamumProjectStage.Prepare && _innerContainer.Count == 0;

        public bool ShouldBigThreats => Stage >= DreamumProjectStage.InProgress && CompDreamumTower.Props.bigThreatActivatePct.IncludesEpsilon(CompDreamumTower.ProgressPct);
        public bool ShouldDreamumHaze => Stage >= DreamumProjectStage.InProgress && CompDreamumTower.ProgressPct >= CompDreamumTower.Props.hazeActivatePct;


        public CompDreamumTower CompDreamumTower
        {
            get
            {
                if (_compDreamumTower == null)
                {
                    _compDreamumTower = GetComp<CompDreamumTower>();
                }
                return _compDreamumTower;
            }
        }
        private CompDreamumTower _compDreamumTower;

        public int GraphicIndex
        {
            get
            {
                if (_stage < DreamumProjectStage.InProgress)
                {
                    return 0;
                }

                var index = 0;
                var comp = CompDreamumTower;
                var pct = comp.ProgressPct;
                for (int i = 0; i < comp.Props.graphicChangeProgressPct.Count; ++i)
                {
                    if (pct < comp.Props.graphicChangeProgressPct[i])
                    {
                        break;
                    }

                    index++;
                }

                return index;
            }
        }

        private ManaFluxNetworkNode _manaFluxNode;
        private int _progressTicks;
        private ThingOwner _innerContainer;
        private int _growthIndex;

        public Building_DreamumAltar()
        {
            _manaFluxNode = new ManaFluxNetworkNode(this);
            _innerContainer = new ThingOwner<Thing>(this, oneStackOnly: true);
        }

        public override void ExposeData()
        {
            base.ExposeData();

            Scribe_Values.Look(ref _stage, "stage");
            Scribe_Deep.Look(ref _manaFluxNode, "manaFluxNode");
            Scribe_Values.Look(ref _progressTicks, "progressTicks");
            Scribe_Deep.Look(ref _innerContainer, "innerContainer", this);
            Scribe_Values.Look(ref _growthIndex, "growthIndex");

            if (Scribe.mode == LoadSaveMode.LoadingVars)
            {
                _manaFluxNode.manaAcceptor = this;
            }
        }

        public override void Tick()
        {
            base.Tick();

            switch (_stage)
            {
                case DreamumProjectStage.Prepare:
                    if (_innerContainer.Any && _manaFluxNode.mana >= ManaExtension.manaCapacity - 1)
                    {
                        _stage = DreamumProjectStage.InProgress;

                        Find.LetterStack.ReceiveLetter(
                            LocalizeString_Letter.VV_Letter_DreamumAltarProgressStartLabel.Translate(),
                            LocalizeString_Letter.VV_Letter_DreamumAltarProgressStart.Translate(),
                            LetterDefOf.NeutralEvent,
                            this);
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
                var command_startProgress = new Command_Action();
                command_startProgress.defaultLabel = LocalizeString_Command.VV_Command_StartProgressDreamumVictory.Translate();
                command_startProgress.defaultDesc = LocalizeString_Command.VV_Command_StartProgressDreamumVictoryDesc.Translate();
                command_startProgress.hotKey = KeyBindingDefOf.Misc1;
                command_startProgress.icon = StartCommandTex;
                command_startProgress.action = () =>
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

                yield return command_startProgress;
            }

            if (DebugSettings.godMode)
            {
                var command_fillMana = new Command_Action();
                command_fillMana.defaultLabel = "DEV: Fill Mana 100%";
                command_fillMana.action = () =>
                {
                    _manaFluxNode.mana = ManaExtension.manaCapacity;
                };

                yield return command_fillMana;
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

        public void GetChildHolders(List<IThingHolder> outChildren)
        {
            ThingOwnerUtility.AppendThingHoldersFromThings(outChildren, GetDirectlyHeldThings());
        }

        public ThingOwner GetDirectlyHeldThings()
        {
            return _innerContainer;
        }

        public void CompleteProgress()
        {
            if (Stage == DreamumProjectStage.InProgress)
            {
                _stage = DreamumProjectStage.Completed;

                Find.LetterStack.ReceiveLetter(
                    LocalizeString_Letter.VV_Letter_DreamumAltarProgressCompleteLabel.Translate(),
                    LocalizeString_Letter.VV_Letter_DreamumAltarProgressComplete.Translate(),
                    LetterDefOf.PositiveEvent,
                    this);
            }
        }
    }
}
