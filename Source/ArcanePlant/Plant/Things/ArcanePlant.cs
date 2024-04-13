using RimWorld;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using Verse;

namespace VVRace
{
    [StaticConstructorOnStartup]
    public class ArcanePlant : ManaAcceptor
    {
        public const float ManaByFertilizer = 20f;

        private static List<ThingDef> _allArcanePlantDefs;
        public static List<ThingDef> AllArcanePlantDefs
        {
            get
            {
                if (_allArcanePlantDefs == null)
                {
                    _allArcanePlantDefs = DefDatabase<ThingDef>.AllDefsListForReading.Where(def => def.GetModExtension<ArcanePlantExtension>() != null).ToList();
                }

                return _allArcanePlantDefs;
            }
        }

        private ArcanePlantExtension _arcanePlantExtension;
        public ArcanePlantExtension ArcanePlantModExtension
        {
            get
            {
                if (_arcanePlantExtension == null)
                {
                    _arcanePlantExtension = def.GetModExtension<ArcanePlantExtension>();
                }

                return _arcanePlantExtension;
            }
        }

        public override bool HasManaFlux => true;

        public override ManaFluxNetwork ManaFluxNetwork { get; set; }
        public override ManaFluxNetworkNode ManaFluxNode => _manaFluxNode;
        private ManaFluxNetworkNode _manaFluxNode;

        private int _zeroManaTicks = 0;

        private bool _fertilizeAutoActivated = true;
        public bool FertilizeAutoActivated => _fertilizeAutoActivated;

        private int _fertilizeAutoThreshold = 0;
        public int FertilizeAutoThreshold
        {
            get => (int)Mathf.Clamp(_fertilizeAutoThreshold, 0, ManaExtension.manaCapacity - ManaByFertilizer);
            set => _fertilizeAutoThreshold = value;
        }

        public int RequiredFertilizerToFullyRecharge
        {
            get
            {
                return Mathf.CeilToInt((ManaExtension.manaCapacity - _manaFluxNode.mana) / ManaByFertilizer);
            }
        }

        public bool ShouldAutoFertilizeNowIgnoringManaPct
        {
            get
            {
                return !this.IsBurning() && Map.designationManager.DesignationOn(this, DesignationDefOf.Uninstall) == null;
            }
        }

        protected virtual bool CanFlip => true;

        private bool _forceMinify = false;

        public ArcanePlant()
        {
            _manaFluxNode = new ManaFluxNetworkNode(this);
        }

        public override void ExposeData()
        {
            base.ExposeData();

            Scribe_Deep.Look(ref _manaFluxNode, "manaFluxNode");
            Scribe_Values.Look(ref _zeroManaTicks, "zeroManaTicks");
            Scribe_Values.Look(ref _fertilizeAutoActivated, "fertilizeAutoActivated", defaultValue: true);
            Scribe_Values.Look(ref _fertilizeAutoThreshold, "fertilizeAutoThreshold", defaultValue: 0);
            Scribe_Values.Look(ref _forceMinify, "forceMinify");

            if (Scribe.mode == LoadSaveMode.LoadingVars)
            {
                _manaFluxNode.manaAcceptor = this;
            }
        }

        public override void PostMake()
        {
            base.PostMake();

            FertilizeAutoThreshold = Mathf.FloorToInt(ManaExtension.manaCapacity * 0.1f);
        }

        public override void Print(SectionLayer layer)
        {
            try
            {
                Rand.PushState();
                Rand.Seed = base.thingIDNumber.GetHashCode();

                var thingTrueCenter = this.TrueCenter();

                var drawSize = def.graphicData.drawSize;

                bool isShift = false;
                var zero = thingTrueCenter + new Vector3(0f, 0f, 0.11f);
                if (zero.z - 0.5f < Position.z)
                {
                    zero.z = Position.z + 0.5f;
                    isShift = true;
                }

                var scale = Rand.Range(0.85f, 1.15f);
                drawSize.Scale(new Vector2(scale, scale));

                var isFlipUV = CanFlip ? Rand.Bool : false;
                var material = Graphic.MatSingleFor(this);
                Graphic.TryGetTextureAtlasReplacementInfo(material, def.category.ToAtlasGroup(), isFlipUV, vertexColors: false, out material, out var uvs, out var _);

                var colors = new Color32[4];
                colors[1].a = (colors[2].a = 25);
                colors[0].a = (colors[3].a = 0);

                Printer_Plane.PrintPlane(
                    size: drawSize,
                    layer: layer,
                    center: zero,
                    mat: material,
                    rot: 0f,
                    flipUv: isFlipUV,
                    uvs: uvs,
                    colors: colors,
                    //topVerticesAltitudeBias: 0.1f,
                    uvzPayload: this.HashOffset() % 1024);

                if (def.graphicData.shadowData != null)
                {
                    Vector3 center = thingTrueCenter + def.graphicData.shadowData.offset;
                    if (isShift)
                    {
                        center.z = base.Position.ToVector3Shifted().z + def.graphicData.shadowData.offset.z;
                    }

                    center.y -= 3f / 74f;
                    Vector3 volume = def.graphicData.shadowData.volume;
                    Printer_Shadow.PrintShadow(layer, center, volume, Rot4.North);
                }
            }
            finally
            {
                Rand.PopState();
            }

            PostPrint(layer);
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

        public override string GetInspectStringLowPriority()
        {
            return null;
        }

        private static readonly Texture2D SetTargetFuelLevelCommand = ContentFinder<Texture2D>.Get("UI/Commands/SetTargetFuelLevel");
        private static readonly Texture2D SetFertilizeAutoActivated = ContentFinder<Texture2D>.Get("UI/Commands/VV_SetFertilizeAutoActivated");
        public override IEnumerable<Gizmo> GetGizmos()
        {
            var gizmos = base.GetGizmos();
            if (gizmos != null)
            {
                foreach (var gizmo in gizmos)
                {
                    if (gizmo is Designator_Install)
                    {
                        yield return ArcanePlantDesignatorCache.GetReplantDesignator(def);
                        continue;
                    }

                    yield return gizmo;
                }
            }

            if (Spawned && Faction != null && Faction.IsPlayer)
            {
                var CommandFertilizeAutoActivated = new Command_Toggle();
                CommandFertilizeAutoActivated.defaultLabel = LocalizeString_Command.CommandFertilizeAutoActivated.Translate();
                CommandFertilizeAutoActivated.defaultDesc = LocalizeString_Command.CommandFertilizeAutoActivatedDesc.Translate();
                CommandFertilizeAutoActivated.icon = SetFertilizeAutoActivated;
                CommandFertilizeAutoActivated.iconOffset = new Vector2(0f, -0.08f);

                CommandFertilizeAutoActivated.isActive = () => _fertilizeAutoActivated;
                CommandFertilizeAutoActivated.toggleAction = () =>
                {
                    _fertilizeAutoActivated = !_fertilizeAutoActivated;
                };
                yield return CommandFertilizeAutoActivated;

                if (_fertilizeAutoActivated)
                {
                    var commandSetFertilizeAutoThreshold = new Command_SetFertilizeAutoThreshold();
                    commandSetFertilizeAutoThreshold.plant = this;
                    commandSetFertilizeAutoThreshold.defaultLabel = LocalizeString_Command.CommandSetFertilizeAutoThreshold.Translate();
                    commandSetFertilizeAutoThreshold.defaultDesc = LocalizeString_Command.CommandSetFertilizeAutoThresholdDesc.Translate();
                    commandSetFertilizeAutoThreshold.icon = SetTargetFuelLevelCommand;
                    yield return commandSetFertilizeAutoThreshold;
                }
            }

            if (DebugSettings.godMode)
            {
                Command_Action commandAddMana = new Command_Action();
                commandAddMana.defaultLabel = "DEV: Add mana +10";
                commandAddMana.action = () =>
                {
                    _manaFluxNode.mana = Mathf.Clamp(_manaFluxNode.mana + 10, 0f, ManaExtension.manaCapacity);
                };

                yield return commandAddMana;
            }

            if (DebugSettings.godMode)
            {
                Command_Action commandSubtractMana = new Command_Action();
                commandSubtractMana.defaultLabel = "DEV: Add mana -10";
                commandSubtractMana.action = () =>
                {
                    _manaFluxNode.mana = Mathf.Clamp(_manaFluxNode.mana - 10, 0f, ManaExtension.manaCapacity);
                };

                yield return commandSubtractMana;
            }
        }

        public override void Tick()
        {
            base.Tick();

            if (!Spawned || Destroyed)
            {
                return;
            }

            if (_forceMinify || !ArcanePlantUtility.CanPlaceArcanePlantToCell(Map, Position, def))
            {
                _forceMinify = false;
                ForceMinifyAndDropDirect();
                return;
            }

            if (this.IsHashIntervalTick(GenTicks.TickRareInterval))
            {
                if ((int)Mana == 0)
                {
                    _zeroManaTicks += GenTicks.TickRareInterval;

                    if (_zeroManaTicks >= ArcanePlantModExtension.zeroManaDurableTicks)
                    {
                        _zeroManaTicks = 0;
                        TakeDamage(new DamageInfo(DamageDefOf.Rotting, ArcanePlantModExtension.zeroManaDamageByChance.RandomInRange));
                    }
                }
                else if (ManaChargeRatio > 0.1f)
                {
                    HitPoints = Mathf.Clamp(HitPoints + 1, 0, MaxHitPoints);
                }
            }
        }

        public override IEnumerable<StatDrawEntry> SpecialDisplayStats()
        {
            yield return new StatDrawEntry(
                StatCategoryDefOf.Basics,
                LocalizeTexts.StatsReport_Mana.Translate(),
                ManaExtension.manaCapacity.ToString(),
                LocalizeTexts.StatsReport_Mana_Desc.Translate(),
                -20000);

            if (Spawned)
            {
                yield return new StatDrawEntry(
                    StatCategoryDefOf.Basics,
                    LocalizeTexts.StatsReport_ManaFlux.Translate(),
                    _manaFluxNode.LocalManaFluxForInspector.ToString("+0;-#"),
                    LocalizeTexts.StatsReport_ManaFlux_Desc.Translate(),
                    -20001);
            }
        }

        public override bool DeconstructibleBy(Faction faction)
        {
            if (DebugSettings.godMode)
            {
                return true;
            }

            return Faction == faction;
        }

        public void AddMana(float mana)
        {
            _manaFluxNode.mana = Mathf.Clamp(_manaFluxNode.mana + mana, 0f, ManaExtension.manaCapacity);
        }

        public void Notify_TurretVerbShot()
        {
            var manaPerShoot = ArcanePlantModExtension.consumeManaPerVerbShoot;
            if (manaPerShoot > 0)
            {
                AddMana(-manaPerShoot);
            }
        }

        public void ReserveMinify()
        {
            _forceMinify = true;
        }

        private void ForceMinifyAndDropDirect()
        {
            var position = Position;
            var map = Map;
            var minified = this.MakeMinified();
            GenPlace.TryPlaceThing(minified, position, map, ThingPlaceMode.Direct);
        }

    }
}
