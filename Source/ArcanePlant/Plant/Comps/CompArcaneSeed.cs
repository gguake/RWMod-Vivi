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

        public List<IntVec3> SeedlingCells => seedlingCells;
        private List<IntVec3> seedlingCells = new List<IntVec3>();

        public override IEnumerable<Gizmo> CompGetGizmosExtra()
        {
            if (SeedlingCells.Count > 0)
            {
                yield return new Command_Action
                {
                    defaultLabel = "CancelPlantThing".Translate(Props.targetPlantDef),
                    defaultDesc = "CancelPlantThingDesc".Translate(Props.targetPlantDef),
                    icon = CancelCommandTex,
                    hotKey = KeyBindingDefOf.Designator_Cancel,
                    action = () =>
                    {
                        seedlingCells.Clear();
                    }
                };
            }

            if (SeedlingCells.Count < parent.stackCount)
            {
                yield return new Command_Action
                {
                    defaultLabel = "PlantThing".Translate(Props.targetPlantDef),
                    defaultDesc = "PlantThingDesc".Translate(Props.targetPlantDef),
                    icon = Props.targetPlantDef.uiIcon,
                    action = BeginTargeting
                };
            }
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
                        seedlingCells.Add(target.Cell);
                        SoundDefOf.Tick_High.PlayOneShotOnCamera();

                        if (seedlingCells.Count < parent.stackCount)
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
                Props.targetPlantDef.uiIcon, 
                playSoundOnAction: false);
        }

        public override void PostDrawExtraSelectionOverlays()
        {
            for (var i = 0; i < seedlingCells.Count; i++)
            {
                if (seedlingCells[i].IsValid)
                {
                    var v = seedlingCells[i];
                    GenDraw.DrawLineBetween(parent.PositionHeld.ToVector3Shifted(), v.ToVector3Shifted(), AltitudeLayer.Blueprint.AltitudeFor());
                    GhostDrawer.DrawGhostThing(v, Rot4.North, Props.targetPlantDef, Props.targetPlantDef.graphic, Color.white, AltitudeLayer.Blueprint);
                }
            }
        }

        public override void PreAbsorbStack(Thing otherStack, int count)
        {
            var compArcaneSeed = otherStack.TryGetComp<CompArcaneSeed>();
            if (compArcaneSeed == null)
            {
                return;
            }

            var cells = compArcaneSeed.SeedlingCells;
            for (int i = 0; i < cells.Count; i++)
            {
                if (cells[i].IsValid && !seedlingCells.Contains(cells[i]))
                {
                    seedlingCells.Add(cells[i]);
                }
            }
        }

        public override void PostSplitOff(Thing piece)
        {
            var compArcaneSeed = piece.TryGetComp<CompArcaneSeed>();
            if (compArcaneSeed == null)
            {
                return;
            }

            var cells = seedlingCells;
            for (int i = 0; i < cells.Count; i++)
            {
                if (cells[i].IsValid && !compArcaneSeed.seedlingCells.Contains(cells[i]))
                {
                    compArcaneSeed.seedlingCells.Add(cells[i]);
                }
            }
        }

        public AcceptanceReport CanSowAt(IntVec3 c, Map map)
        {
            if (!c.IsValid)
            {
                return false;
            }

            if (seedlingCells.Contains(c))
            {
                return false;
            }

            var canPlaceArcanePlantToCell = ArcanePlantUtility.CanPlaceArcanePlantToCell(map, c, Props.targetPlantDef);
            if (canPlaceArcanePlantToCell.Accepted == false)
            {
                return canPlaceArcanePlantToCell;
            }
            
            return true;
        }

        public override void PostExposeData()
        {
            base.PostExposeData();

            Scribe_Collections.Look(ref seedlingCells, "seedlingCells", LookMode.Value);
            if (Scribe.mode == LoadSaveMode.PostLoadInit && seedlingCells == null)
            {
                seedlingCells = new List<IntVec3>();
            }
        }

        public bool ValidateTarget(LocalTargetInfo target)
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
