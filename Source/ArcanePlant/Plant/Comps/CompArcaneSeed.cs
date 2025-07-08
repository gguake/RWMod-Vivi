using RimWorld;
using System.Collections.Generic;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace VVRace
{
    public class CompProperties_ArcaneSeed : CompProperties
    {
        public ThingDef targetPlantDef;

        public CompProperties_ArcaneSeed()
        {
            compClass = typeof(CompArcaneSeed);
        }
    }

    [StaticConstructorOnStartup]
    public class CompArcaneSeed : ThingComp
    {
        private static readonly Texture2D CancelCommandTex = ContentFinder<Texture2D>.Get("UI/Designators/Cancel");

        public CompProperties_ArcaneSeed Props => (CompProperties_ArcaneSeed)props;

        public List<IntVec3> SeedlingCells => _seedlingCells;
        private List<IntVec3> _seedlingCells = new List<IntVec3>();

        public override void PostDestroy(DestroyMode mode, Map previousMap)
        {
            base.PostDestroy(mode, previousMap);

            previousMap?.GetComponent<ArcanePlantMapComponent>()?.Notify_ArcaneSeedPlantCanceled(parent);
        }

        public override IEnumerable<Gizmo> CompGetGizmosExtra()
        {
            if (SeedlingCells.Count > 0)
            {
                yield return new Command_Action
                {
                    defaultLabel = "CancelPlantThing".Translate(Props.targetPlantDef ?? parent.def),
                    defaultDesc = "CancelPlantThingDesc".Translate(Props.targetPlantDef ?? parent.def),
                    icon = CancelCommandTex,
                    hotKey = KeyBindingDefOf.Designator_Cancel,
                    action = () =>
                    {
                        parent.MapHeld.GetComponent<ArcanePlantMapComponent>()?.Notify_ArcaneSeedPlantCanceled(parent);
                        _seedlingCells.Clear();
                    }
                };
            }

            if (SeedlingCells.Count < parent.stackCount)
            {
                var commandPlant = new Command_Action
                {
                    defaultLabel = "PlantThing".Translate(Props.targetPlantDef ?? parent.def),
                    defaultDesc = "PlantThingDesc".Translate(Props.targetPlantDef ?? parent.def),
                    icon = Props.targetPlantDef?.uiIcon ?? parent.def.uiIcon,
                    hotKey = KeyBindingDefOf.Misc1,
                    action = BeginTargeting,
                };

                if (!VVResearchProjectDefOf.VV_ArcaneBotany.IsFinished)
                {
                    commandPlant.Disable("NotStudied".Translate(VVResearchProjectDefOf.VV_ArcaneBotany.LabelCap));
                }

                yield return commandPlant;
            }
        }

        public override void PostDrawExtraSelectionOverlays()
        {
            for (var i = 0; i < _seedlingCells.Count; i++)
            {
                GenDraw.DrawLineBetween(
                    parent.PositionHeld.ToVector3Shifted(), 
                    _seedlingCells[i].ToVector3Shifted(), 
                    AltitudeLayer.Blueprint.AltitudeFor());

                GhostDrawer.DrawGhostThing(
                    _seedlingCells[i], 
                    Rot4.North, 
                    VVThingDefOf.VV_ArcanePlantSeedling, 
                    VVThingDefOf.VV_ArcanePlantSeedling.graphic, 
                    Color.white, 
                    AltitudeLayer.Blueprint);
            }
        }

        public override void PreAbsorbStack(Thing otherStack, int count)
        {
            var otherComp = otherStack.TryGetComp<CompArcaneSeed>();
            if (otherComp == null)
            {
                return;
            }

            _seedlingCells.AddRange(otherComp.SeedlingCells);
        }

        public override void PostSplitOff(Thing piece)
        {
            var otherComp = piece.TryGetComp<CompArcaneSeed>();
            if (otherComp == null)
            {
                return;
            }

            for (int i = 0; i < SeedlingCells.Count; ++i)
            {
                if (SeedlingCells[i].IsValid && !otherComp.SeedlingCells.Contains(SeedlingCells[i]))
                {
                    otherComp.SeedlingCells.Add(SeedlingCells[i]);
                }
            }
        }

        public override void PostExposeData()
        {
            base.PostExposeData();

            Scribe_Collections.Look(ref _seedlingCells, "seedlingCells", LookMode.Value);
        }

        public AcceptanceReport CanSowAt(IntVec3 c, Map map)
        {
            if (!c.IsValid)
            {
                return false;
            }

            var canPlaceArcanePlantToCell = ArcanePlantUtility.CanPlaceArcanePlantToCell(map, c, VVThingDefOf.VV_ArcanePlantSeedling);
            if (canPlaceArcanePlantToCell.Accepted == false)
            {
                return canPlaceArcanePlantToCell;
            }

            return true;
        }

        private void BeginTargeting()
        {
            Find.Targeter.BeginTargeting(
                new TargetingParameters
                {
                    canTargetPawns = false,
                    canTargetLocations = true
                },
                (target) =>
                {
                    if (ValidateTarget(target))
                    {
                        var cell = target.Cell;
                        _seedlingCells.Add(cell);

                        if (_seedlingCells.Count == 1)
                        {
                            parent.MapHeld.GetComponent<ArcanePlantMapComponent>()?.Notify_ArcaneSeedPlantReserved(parent);
                        }

                        SoundDefOf.Tick_High.PlayOneShotOnCamera();

                        if (_seedlingCells.Count < parent.stackCount)
                        {
                            BeginTargeting();
                        }
                    }
                    else
                    {
                        BeginTargeting();
                    }
                },
                (target) =>
                {
                    if (CanSowAt(target.Cell, parent.MapHeld).Accepted)
                    {
                        GenDraw.DrawTargetHighlight(target);
                    }
                },
                (_) => true,
                null,
                null,
                parent.def.uiIcon,
                playSoundOnAction: false);
        }

        private bool ValidateTarget(LocalTargetInfo target)
        {
            var canSowAt = CanSowAt(target.Cell, parent.MapHeld);
            if (canSowAt.Accepted == false)
            {
                if (!canSowAt.Reason.NullOrEmpty())
                {
                    Messages.Message(canSowAt.Reason, parent, MessageTypeDefOf.RejectInput, historical: false);
                }

                return false;
            }

            return true;
        }
    }
}
