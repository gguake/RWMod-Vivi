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

        private ArcanePlantExtension _defModExtension;
        public ArcanePlantExtension ArcanePlantModExtension
        {
            get
            {
                if (_defModExtension == null)
                {
                    _defModExtension = def.GetModExtension<ArcanePlantExtension>();
                }

                return _defModExtension;
            }
        }

        public override ManaFluxNetwork ManaFluxNetwork { get; set; }
        public override ManaFluxNetworkNode ManaFluxNode => _manaFluxNode;
        private ManaFluxNetworkNode _manaFluxNode;

        public bool IsFullMana => _manaFluxNode.mana >= ArcanePlantModExtension.manaCapacity;
        public float ManaChargeRatio => _manaFluxNode.mana / ArcanePlantModExtension.manaCapacity;
        public float Mana => _manaFluxNode.mana;

        private int _zeroManaTicks = 0;

        private bool _fertilizeAutoActivated = true;
        public bool FertilizeAutoActivated => _fertilizeAutoActivated;

        private int _fertilizeAutoThreshold = 0;
        public int FertilizeAutoThreshold
        {
            get => (int)Mathf.Clamp(_fertilizeAutoThreshold, 0, ArcanePlantModExtension.manaCapacity - ManaByFertilizer);
            set => _fertilizeAutoThreshold = value;
        }

        public int RequiredFertilizerToFullyRecharge
        {
            get
            {
                return Mathf.CeilToInt((ArcanePlantModExtension.manaCapacity - _manaFluxNode.mana) / ManaByFertilizer);
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

        public ArcanePlant()
        {
            _manaFluxNode = new ManaFluxNetworkNode()
            {
                manaAcceptor = this
            };
        }

        public override void ExposeData()
        {
            base.ExposeData();

            Scribe_Deep.Look(ref _manaFluxNode, "manaFluxNode");
            Scribe_Values.Look(ref _zeroManaTicks, "zeroManaTicks");
            Scribe_Values.Look(ref _fertilizeAutoActivated, "fertilizeAutoActivated", defaultValue: true);
            Scribe_Values.Look(ref _fertilizeAutoThreshold, "fertilizeAutoThreshold", defaultValue: 0);

            if (Scribe.mode == LoadSaveMode.LoadingVars)
            {
                _manaFluxNode.manaAcceptor = this;
            }
        }

        public override void PostMake()
        {
            base.PostMake();

            _manaFluxNode.mana = ArcanePlantModExtension.manaCapacity * ArcanePlantModExtension.initialManaPercent;
            FertilizeAutoThreshold = Mathf.FloorToInt(ArcanePlantModExtension.manaCapacity * 0.1f);
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

            sb.Append(LocalizeTexts.InspectorPlantMana.Translate((int)_manaFluxNode.mana, ArcanePlantModExtension.manaCapacity));

            if (Spawned)
            {
                var manaFlux = _manaFluxNode.LocalManaFluxForInspector;
                if (manaFlux != 0)
                {
                    sb.Append(" ");
                    sb.Append(LocalizeTexts.InspectorPlantManaFlux.Translate(manaFlux.ToString("+0;-#")));
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
                Command_Action commandAddMana = new Command_Action();
                commandAddMana.defaultLabel = "DEV: Add mana +10";
                commandAddMana.action = () =>
                {
                    _manaFluxNode.mana = Mathf.Clamp(_manaFluxNode.mana + 10, 0f, ArcanePlantModExtension.manaCapacity);
                };

                yield return commandAddMana;
            }

            if (DebugSettings.godMode)
            {
                Command_Action commandSubtractMana = new Command_Action();
                commandSubtractMana.defaultLabel = "DEV: Add mana -10";
                commandSubtractMana.action = () =>
                {
                    _manaFluxNode.mana = Mathf.Clamp(_manaFluxNode.mana - 10, 0f, ArcanePlantModExtension.manaCapacity);
                };

                yield return commandSubtractMana;
            }
        }

        public override void Tick()
        {
            base.Tick();

            if (Destroyed)
            {
                return;
            }

            if (Spawned && ManaFluxNetwork != null && (Find.TickManager.TicksGame + ManaFluxNetwork.NetworkHash) % 60 == 0)
            {
                ManaFluxNetwork.Tick();
            }

            if (Spawned && this.IsHashIntervalTick(GenTicks.TickRareInterval))
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

                if (!ArcanePlantUtility.CanPlaceArcanePlantToCell(Map, Position, def))
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

            if (Spawned && ManaFluxNetwork != null)
            {
                ManaFluxNetwork.Tick();
            }

            if (Spawned)
            {
                if (!ArcanePlantUtility.CanPlaceArcanePlantToCell(Map, Position, def))
                {
                    ForceMinifyAndDropDirect();
                    return;
                }

                if ((int)Mana == 0)
                {
                    _zeroManaTicks += GenTicks.TickRareInterval;

                    if (_zeroManaTicks >= ArcanePlantModExtension.zeroManaDurableTicks)
                    {
                        _zeroManaTicks = 0;
                        TakeDamage(new DamageInfo(DamageDefOf.Rotting, ArcanePlantModExtension.zeroManaDamageByChance.RandomInRange));
                    }
                }
                else if (ManaChargeRatio > 0.1f && HitPoints < MaxHitPoints)
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

            if (Spawned && ManaFluxNetwork != null)
            {
                ManaFluxNetwork.Tick();
            }

            if (Spawned)
            {
                if (!ArcanePlantUtility.CanPlaceArcanePlantToCell(Map, Position, def))
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
                LocalizeTexts.StatsReport_Mana.Translate(),
                ArcanePlantModExtension.manaCapacity.ToString(),
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
            _manaFluxNode.mana = Mathf.Clamp(_manaFluxNode.mana + mana, 0f, ArcanePlantModExtension.manaCapacity);
        }

        public void Notify_TurretVerbShot()
        {
            var manaPerShoot = ArcanePlantModExtension.consumeManaPerVerbShoot;
            if (manaPerShoot > 0)
            {
                AddMana(-manaPerShoot);
            }
        }

        private void ForceMinifyAndDropDirect()
        {
            var position = Position;
            var map = Map;
            var minified = this.MakeMinified();
            GenPlace.TryPlaceThing(minified, position, map, ThingPlaceMode.Direct);
        }

        private IEnumerable<ManaAcceptor> AdjacentManaAcceptor
        {
            get
            {
                foreach (var adjacent in GenAdjFast.AdjacentCellsCardinal(Position))
                {

                    var gridCell = ManaFluxGrid[adjacent];
                    if (gridCell.arcanePlant != null)
                    {
                        yield return gridCell.arcanePlant;
                    }
                    else if (gridCell.transmitter != null)
                    {
                        yield return gridCell.transmitter;
                    }
                }
            }
        }

        private IEnumerable<ManaAcceptor> AdjacentManaTransmitter
        {
            get
            {
                var gridCell = ManaFluxGrid[Position];
                if (gridCell.transmitter != null)
                {
                    yield return gridCell.transmitter;
                }

                foreach (var adjacent in GenAdjFast.AdjacentCellsCardinal(Position))
                {
                    gridCell = ManaFluxGrid[adjacent];
                    if (gridCell.transmitter != null)
                    {
                        yield return gridCell.transmitter;
                    }
                }
            }
        }

    }
}
