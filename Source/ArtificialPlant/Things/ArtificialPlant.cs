using RimWorld;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using Verse;

namespace VVRace
{
    [StaticConstructorOnStartup]
    public class ArtificialPlant : EnergyAcceptor
    {
        public const float EnergyByFertilizer = 10f;

        private static List<ThingDef> _allArtificialPlantDefs;
        public static IEnumerable<ThingDef> AllArtificialPlantDefs
        {
            get
            {
                if (_allArtificialPlantDefs == null)
                {
                    _allArtificialPlantDefs = DefDatabase<ThingDef>.AllDefsListForReading.Where(def => def.GetModExtension<ArtificialPlantModExtension>() != null).ToList();
                }

                return _allArtificialPlantDefs;
            }
        }

        private ArtificialPlantModExtension _defModExtension;
        public ArtificialPlantModExtension ArtificialPlantModExtension
        {
            get
            {
                if (_defModExtension == null)
                {
                    _defModExtension = def.GetModExtension<ArtificialPlantModExtension>();
                }

                return _defModExtension;
            }
        }

        public override EnergyFluxNetwork EnergyFluxNetwork { get; set; }
        public override EnergyFluxNetworkNode EnergyFluxNode => _energyNode;
        private EnergyFluxNetworkNode _energyNode;

        public bool IsFullEnergy => _energyNode.energy >= ArtificialPlantModExtension.energyCapacity;
        public float EnergyChargeRatio => _energyNode.energy / ArtificialPlantModExtension.energyCapacity;
        public float Energy => _energyNode.energy;

        private int _zeroEnergyTicks = 0;

        private bool _fertilizeAutoActivated = true;
        public bool FertilizeAutoActivated => _fertilizeAutoActivated;

        private int _fertilizeAutoThreshold = 0;
        public int FertilizeAutoThreshold
        {
            get => (int)Mathf.Clamp(_fertilizeAutoThreshold, 0, ArtificialPlantModExtension.energyCapacity - EnergyByFertilizer);
            set => _fertilizeAutoThreshold = value;
        }

        public int RequiredFertilizerToFullyRecharge
        {
            get
            {
                return Mathf.CeilToInt((ArtificialPlantModExtension.energyCapacity - _energyNode.energy) / EnergyByFertilizer);
            }
        }

        public bool ShouldAutoFertilizeNowIgnoringEnergyPct
        {
            get
            {
                return !this.IsBurning() && Map.designationManager.DesignationOn(this, DesignationDefOf.Uninstall) == null;
            }
        }

        protected virtual bool CanFlip => true;

        public ArtificialPlant()
        {
            _energyNode = new EnergyFluxNetworkNode()
            {
                energyAcceptor = this
            };
        }

        public override void ExposeData()
        {
            base.ExposeData();

            Scribe_Deep.Look(ref _energyNode, "energyNode");
            Scribe_Values.Look(ref _zeroEnergyTicks, "zeroEnergyTicks");
            Scribe_Values.Look(ref _fertilizeAutoActivated, "fertilizeAutoActivated", defaultValue: true);
            Scribe_Values.Look(ref _fertilizeAutoThreshold, "fertilizeAutoThreshold", defaultValue: 0);

            if (Scribe.mode == LoadSaveMode.LoadingVars)
            {
                _energyNode.energyAcceptor = this;
            }
        }

        public override void PostMake()
        {
            base.PostMake();

            _energyNode.energy = ArtificialPlantModExtension.energyCapacity * ArtificialPlantModExtension.initialEnergyPercent;
        }

        public override void Print(SectionLayer layer)
        {
            try
            {
                Rand.PushState();
                Rand.Seed = base.thingIDNumber.GetHashCode();

                var thingTrueCenter = this.TrueCenter();
                var drawSize = def.graphicData.drawSize.x;

                bool isShift = false;
                var zero = thingTrueCenter + new Vector3(0f, 0f, 0.11f);
                if (zero.z - 0.5f < Position.z)
                {
                    zero.z = Position.z + 0.5f;
                    isShift = true;
                }

                bool isFlipUV = CanFlip ? Rand.Bool : false;
                var material = Graphic.MatSingleFor(this);
                Graphic.TryGetTextureAtlasReplacementInfo(material, def.category.ToAtlasGroup(), isFlipUV, vertexColors: false, out material, out var uvs, out var _);

                var colors = new Color32[4];
                colors[1].a = (colors[2].a = 25);
                colors[0].a = (colors[3].a = 0);

                Printer_Plane.PrintPlane(
                    size: new Vector2(drawSize, drawSize),
                    layer: layer,
                    center: zero,
                    mat: material,
                    rot: 0f,
                    flipUv: isFlipUV,
                    uvs: uvs,
                    colors: colors,
                    topVerticesAltitudeBias: 0.1f,
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

            foreach (var thing in AdjacentEnergyTransmitter)
            {
                PrintEnergyWirePieceConnecting(layer, thing, false);
                return;
            }
        }

        public override void PrintForEnergyGrid(SectionLayer layer)
        {
            PrintOverlayConnectorBaseFor(layer);

            foreach (var thing in AdjacentEnergyAcceptor)
            {
                PrintEnergyWirePieceConnecting(layer, thing, true);
            }
        }

        public override string GetInspectString()
        {
            var sb = new StringBuilder(base.GetInspectString());
            if (sb.Length > 0)
            {
                sb.Append("\n");
            }

            sb.Append(LocalizeTexts.InspectorPlantEnergy.Translate((int)_energyNode.energy, ArtificialPlantModExtension.energyCapacity));

            if (Spawned)
            {
                var energyFlux = _energyNode.LocalEnergyFluxForInspector;
                if (energyFlux != 0)
                {
                    sb.Append(" ");
                    sb.Append(LocalizeTexts.InspectorPlantEnergyFlux.Translate(energyFlux.ToString("+0;-#")));
                }

                if (DebugSettings.godMode && EnergyFluxNetwork != null)
                {
                    sb.AppendLine();
                    sb.Append($"flux network: {EnergyFluxNetwork.NetworkHash}");
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
            foreach (var gizmo in base.GetGizmos())
            {
                if (gizmo is Designator_Install)
                {
                    yield return ArtificialPlantDesignatorCache.GetReplantDesignator(def);
                    continue;
                }

                yield return gizmo;
            }

            if (Spawned && Faction.IsPlayer)
            {
                var CommandFertilizeAutoActivated = new Command_Toggle();
                CommandFertilizeAutoActivated.defaultLabel = LocalizeTexts.CommandFertilizeAutoActivated.Translate();
                CommandFertilizeAutoActivated.defaultDesc = LocalizeTexts.CommandFertilizeAutoActivatedDesc.Translate();
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
                    commandSetFertilizeAutoThreshold.defaultLabel = LocalizeTexts.CommandSetFertilizeAutoThreshold.Translate();
                    commandSetFertilizeAutoThreshold.defaultDesc = LocalizeTexts.CommandSetFertilizeAutoThresholdDesc.Translate();
                    commandSetFertilizeAutoThreshold.icon = SetTargetFuelLevelCommand;
                    yield return commandSetFertilizeAutoThreshold;
                }
            }

            if (DebugSettings.godMode)
            {
                Command_Action command_replaceInstantly = new Command_Action();
                command_replaceInstantly.defaultLabel = "DEV: Add energy +10";
                command_replaceInstantly.action = () =>
                {
                    _energyNode.energy = Mathf.Clamp(_energyNode.energy + 10, 0f, ArtificialPlantModExtension.energyCapacity);
                };

                yield return command_replaceInstantly;
            }

            if (DebugSettings.godMode)
            {
                Command_Action command_replaceInstantly = new Command_Action();
                command_replaceInstantly.defaultLabel = "DEV: Add energy -10";
                command_replaceInstantly.action = () =>
                {
                    _energyNode.energy = Mathf.Clamp(_energyNode.energy - 10, 0f, ArtificialPlantModExtension.energyCapacity);
                };

                yield return command_replaceInstantly;
            }
        }

        public override void Tick()
        {
            base.Tick();

            if (Destroyed)
            {
                return;
            }

            if (Spawned && EnergyFluxNetwork != null && (Find.TickManager.TicksGame + EnergyFluxNetwork.NetworkHash) % 60 == 0)
            {
                EnergyFluxNetwork.Tick();
            }

            if (Spawned && this.IsHashIntervalTick(GenTicks.TickRareInterval))
            {
                if ((int)Energy == 0)
                {
                    _zeroEnergyTicks += GenTicks.TickRareInterval;

                    if (_zeroEnergyTicks >= ArtificialPlantModExtension.zeroEnergyDurableTicks)
                    {
                        _zeroEnergyTicks = 0;
                        TakeDamage(new DamageInfo(DamageDefOf.Rotting, ArtificialPlantModExtension.zeroEnergyDamageByChance.RandomInRange));
                    }
                }
                else if (EnergyChargeRatio > 0.1f)
                {
                    HitPoints = Mathf.Clamp(HitPoints + 1, 0, MaxHitPoints);
                }

                if (!CanPlaceCell(Position))
                {
                    ForceMinifyAndDropDirect();
                    return;
                }
            }
        }

        public override void TickRare()
        {
            base.TickRare();

            if (Destroyed)
            {
                return;
            }

            if (Spawned && EnergyFluxNetwork != null)
            {
                EnergyFluxNetwork.Tick();
            }

            if (Spawned)
            {
                if (!CanPlaceCell(Position))
                {
                    ForceMinifyAndDropDirect();
                    return;
                }

                if ((int)Energy == 0)
                {
                    _zeroEnergyTicks += GenTicks.TickRareInterval;

                    if (_zeroEnergyTicks >= ArtificialPlantModExtension.zeroEnergyDurableTicks)
                    {
                        _zeroEnergyTicks = 0;
                        TakeDamage(new DamageInfo(DamageDefOf.Rotting, ArtificialPlantModExtension.zeroEnergyDamageByChance.RandomInRange));
                    }
                }
                else if (EnergyChargeRatio > 0.1f && HitPoints < MaxHitPoints)
                {
                    HitPoints = Mathf.Clamp(HitPoints + 1, 0, MaxHitPoints);
                }
            }
        }

        public override void TickLong()
        {
            base.TickLong();

            if (Destroyed)
            {
                return;
            }

            if (Spawned && EnergyFluxNetwork != null)
            {
                EnergyFluxNetwork.Tick();
            }

            if (Spawned)
            {
                if (!CanPlaceCell(Position))
                {
                    ForceMinifyAndDropDirect();
                    return;
                }
            }
        }

        public override IEnumerable<StatDrawEntry> SpecialDisplayStats()
        {
            yield return new StatDrawEntry(
                StatCategoryDefOf.Basics,
                LocalizeTexts.StatsReport_Energy.Translate(),
                ArtificialPlantModExtension.energyCapacity.ToString(),
                LocalizeTexts.StatsReport_Energy_Desc.Translate(),
                -20000);

            if (Spawned)
            {
                yield return new StatDrawEntry(
                    StatCategoryDefOf.Basics,
                    LocalizeTexts.StatsReport_EnergyFlux.Translate(),
                    _energyNode.LocalEnergyFluxForInspector.ToString("+0;-#"),
                    LocalizeTexts.StatsReport_EnergyFlux_Desc.Translate(),
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

        public void AddEnergy(float energy)
        {
            _energyNode.energy = Mathf.Clamp(_energyNode.energy + energy, 0f, ArtificialPlantModExtension.energyCapacity);
        }

        public bool CanPlaceCell(IntVec3 cell)
        {
            var blockingThings = Map.thingGrid.ThingsListAtFast(cell);
            var onArtificialPlantPot = blockingThings.Any(v => v is ArtificialPlantPot);

            var terrain = cell.GetTerrain(Map);
            var isWaterPlant = terrain.IsWater && def.terrainAffordanceNeeded != null && terrain.affordances.Contains(def.terrainAffordanceNeeded);
            if (!onArtificialPlantPot && !isWaterPlant && Map.fertilityGrid.FertilityAt(cell) <= 0f)
            {
                return false;
            }

            return true;
        }

        public void Notify_TurretVerbShot()
        {
            var energy = ArtificialPlantModExtension.verbShootEnergy;
            if (energy > 0)
            {
                AddEnergy(-energy);
            }
        }

        private void ForceMinifyAndDropDirect()
        {
            var position = Position;
            var map = Map;
            var minified = this.MakeMinified();
            GenPlace.TryPlaceThing(minified, position, map, ThingPlaceMode.Direct);
        }

        private IEnumerable<EnergyAcceptor> AdjacentEnergyAcceptor
        {
            get
            {
                foreach (var adjacent in GenAdjFast.AdjacentCellsCardinal(Position))
                {

                    var gridCell = EnergyFluxGrid[adjacent];
                    if (gridCell.artificialPlant != null)
                    {
                        yield return gridCell.artificialPlant;
                    }
                    else if (gridCell.transmitter != null)
                    {
                        yield return gridCell.transmitter;
                    }
                }
            }
        }

        private IEnumerable<EnergyAcceptor> AdjacentEnergyTransmitter
        {
            get
            {
                var gridCell = EnergyFluxGrid[Position];
                if (gridCell.transmitter != null)
                {
                    yield return gridCell.transmitter;
                }

                foreach (var adjacent in GenAdjFast.AdjacentCellsCardinal(Position))
                {
                    gridCell = EnergyFluxGrid[adjacent];
                    if (gridCell.transmitter != null)
                    {
                        yield return gridCell.transmitter;
                    }
                }
            }
        }

    }
}
