using RimWorld;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using Verse;

namespace VVRace
{
    [StaticConstructorOnStartup]
    public class ArtificialPlant : Building
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

        public EnergyFluxNetwork EnergyFluxNetwork { get; set; }
        private EnergyFluxNetworkNode _energyNode;

        public bool IsFullEnergy => _energyNode.energy >= ArtificialPlantModExtension.energyCapacity;
        public float EnergyChargeRatio => _energyNode.energy / ArtificialPlantModExtension.energyCapacity;
        public float Energy => _energyNode.energy;

        public int EnergyFlux
        {
            get
            {
                var generatedEnergy = ArtificialPlantModExtension.energyGenerateRule?.CalcEnergy(this, 60000) ?? 0;
                var consumedEnergy = ArtificialPlantModExtension.energyConsumeRule?.CalcEnergy(this, 60000) ?? 0;
                return (int)generatedEnergy - (int)consumedEnergy;
            }
        }

        private int _zeroEnergyTicks = 0;

        private bool _fertilizeAutoActivated = false;
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

        public ArtificialPlant()
        {
            _energyNode = new EnergyFluxNetworkNode()
            {
                plant = this
            };
        }

        public override void ExposeData()
        {
            base.ExposeData();

            Scribe_Deep.Look(ref _energyNode, "energyNode");
            Scribe_Values.Look(ref _zeroEnergyTicks, "zeroEnergyTicks");
            Scribe_Values.Look(ref _fertilizeAutoActivated, "fertilizeAutoActivated");
            Scribe_Values.Look(ref _fertilizeAutoThreshold, "fertilizeAutoThreshold");

            if (Scribe.mode == LoadSaveMode.LoadingVars)
            {
                _energyNode.plant = this;
            }
        }

        public override void PostMake()
        {
            base.PostMake();

            _energyNode.energy = ArtificialPlantModExtension.energyCapacity * ArtificialPlantModExtension.initialEnergyPercent;
        }

        public override void SpawnSetup(Map map, bool respawningAfterLoad)
        {
            base.SpawnSetup(map, respawningAfterLoad);

            var candidates = new HashSet<EnergyFluxNetwork>();
            foreach (var c in GenAdjFast.AdjacentCellsCardinal(this).Where(c => c.InBounds(map)))
            {
                var adjacentPlant = c.GetFirstThing<ArtificialPlant>(map);
                if (adjacentPlant != null)
                {
                    candidates.Add(adjacentPlant.EnergyFluxNetwork);

                _energyNode.connectedNodes.Add(adjacentPlant._energyNode);
                adjacentPlant._energyNode.connectedNodes.Add(_energyNode);
                }
            }

            if (candidates.Count == 0)
            {
                EnergyFluxNetwork = new EnergyFluxNetwork();
                EnergyFluxNetwork.AddPlant(this, _energyNode);
            }
            else if (candidates.Count == 1)
            {
                EnergyFluxNetwork = candidates.First();
                EnergyFluxNetwork.AddPlant(this, _energyNode);
            }
            else
            {
                EnergyFluxNetwork = candidates.First();
                EnergyFluxNetwork.AddPlant(this, _energyNode);

                candidates.Remove(EnergyFluxNetwork);
                EnergyFluxNetwork.MergeNetworks(candidates);
            }
        }

        public override void DeSpawn(DestroyMode mode = DestroyMode.Vanish)
        {
            base.DeSpawn(mode);

            foreach (var node in _energyNode.connectedNodes)
            {
                node.connectedNodes.Remove(_energyNode);
            }
            _energyNode.connectedNodes.Clear();

            if (EnergyFluxNetwork != null)
            {
                EnergyFluxNetwork.RemovePlant(this);
                EnergyFluxNetwork = null;
            }
        }

        public override void Print(SectionLayer layer)
        {
            try
            {
                Rand.PushState();
                Rand.Seed = base.thingIDNumber.GetHashCode();

                Vector3 thingTrueCenter = this.TrueCenter();
                float drawSize = def.graphicData.drawSize.x;

                bool isShift = false;
                var zero = thingTrueCenter + new Vector3(0f, 0f, 0.11f);
                if (zero.z - 0.5f < Position.z)
                {
                    zero.z = Position.z + 0.5f;
                    isShift = true;
                }

                bool isFlipUV = Rand.Bool;
                Material material = Graphic.MatSingleFor(this);
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
                var energyFlux = EnergyFlux;
                if (energyFlux != 0)
                {
                    sb.Append(" ");
                    sb.Append(LocalizeTexts.InspectorPlantEnergyFlux.Translate(energyFlux.ToString("+0;-#")));
                }

                if (FertilizeAutoActivated)
                {
                    sb.AppendLine();
                    sb.Append(LocalizeTexts.InspectorFertilizerAuto.Translate(FertilizeAutoThreshold));
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
                if (gizmo is Designator_Install) { continue; }

                yield return gizmo;
            }

            if (Spawned)
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
            if (Spawned && EnergyFluxNetwork?.ShouldRegenerateNetworkTick >= 0 && GenTicks.TicksGame >= EnergyFluxNetwork.ShouldRegenerateNetworkTick)
            {
                EnergyFluxNetwork.SplitNetworks();
            }

            base.Tick();

            if (Spawned && EnergyFluxNetwork != null && this.IsHashIntervalTick(60))
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
            }
        }

        public override void TickRare()
        {
            base.TickRare();

            if (Spawned && EnergyFluxNetwork != null)
            {
                EnergyFluxNetwork.Tick();
            }

            if (Spawned)
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
                else if (EnergyChargeRatio > 0.1f && HitPoints < MaxHitPoints)
                {
                    HitPoints = Mathf.Clamp(HitPoints + 1, 0, MaxHitPoints);
                }
            }
        }

        public override void TickLong()
        {
            base.TickLong();

            if (Spawned && EnergyFluxNetwork != null)
            {
                EnergyFluxNetwork.Tick();
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
                    EnergyFlux.ToString("+0;-#"),
                    LocalizeTexts.StatsReport_EnergyFlux_Desc.Translate(),
                    -20001);
            }
        }

        public void AddEnergy(float energy)
        {
            _energyNode.energy = Mathf.Clamp(_energyNode.energy + energy, 0f, ArtificialPlantModExtension.energyCapacity);
            _energyNode.nextRefreshTick = 0;
        }

        public void Notify_TurretVerbShot()
        {
            var energy = ArtificialPlantModExtension.verbShootEnergy;
            if (energy > 0)
            {
                AddEnergy(-energy);
            }
        }
    }
}
