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

        public List<Blueprint_PlantSeed> SeedlingBlueprints => _seedlingBlueprints;
        private List<Blueprint_PlantSeed> _seedlingBlueprints = new List<Blueprint_PlantSeed>();

        public override IEnumerable<Gizmo> CompGetGizmosExtra()
        {
            if (SeedlingBlueprints.Count > 0)
            {
                yield return new Command_Action
                {
                    defaultLabel = "CancelPlantThing".Translate(Props.targetPlantDef),
                    defaultDesc = "CancelPlantThingDesc".Translate(Props.targetPlantDef),
                    icon = CancelCommandTex,
                    hotKey = KeyBindingDefOf.Designator_Cancel,
                    action = () =>
                    {
                        _seedlingBlueprints.Clear();
                    }
                };
            }

            if (SeedlingBlueprints.Count < parent.stackCount)
            {
                yield return new Command_Action
                {
                    defaultLabel = "PlantThing".Translate(Props.targetPlantDef),
                    defaultDesc = "PlantThingDesc".Translate(Props.targetPlantDef),
                    icon = Props.targetPlantDef?.uiIcon ?? parent.def.uiIcon,
                    hotKey = KeyBindingDefOf.Misc1,
                    action = BeginTargeting
                };
            }
        }

        public override void PostDrawExtraSelectionOverlays()
        {
            for (var i = 0; i < _seedlingBlueprints.Count; i++)
            {
                GenDraw.DrawLineBetween(
                    parent.PositionHeld.ToVector3Shifted(), 
                    _seedlingBlueprints[i].PositionHeld.ToVector3Shifted(), 
                    AltitudeLayer.Blueprint.AltitudeFor());
            }
        }

        public override void PreAbsorbStack(Thing otherStack, int count)
        {
            var otherComp = otherStack.TryGetComp<CompArcaneSeed>();
            if (otherComp == null)
            {
                return;
            }

            _seedlingBlueprints.AddRange(otherComp.SeedlingBlueprints);
        }

        public override void PostSplitOff(Thing piece)
        {
            var otherComp = piece.TryGetComp<CompArcaneSeed>();
            if (otherComp == null)
            {
                return;
            }

            var splittedCount = _seedlingBlueprints.Count - piece.stackCount;
            if (splittedCount > 0)
            {
                for (int i = 0; i < splittedCount; ++i)
                {
                    _seedlingBlueprints[i].SetSeedToPlant((ThingWithComps)piece);
                    otherComp._seedlingBlueprints.Add(_seedlingBlueprints[i]);
                }

                _seedlingBlueprints.RemoveRange(0, splittedCount);
            }
        }

        public override void PostExposeData()
        {
            base.PostExposeData();

            Scribe_Collections.Look(ref _seedlingBlueprints, "seedlingBlueprints", LookMode.Reference);
        }

        public override void PrePreTraded(TradeAction action, Pawn playerNegotiator, ITrader trader)
        {
            for (int i = 0; i < _seedlingBlueprints.Count; ++i)
            {
                _seedlingBlueprints[i].Destroy();
            }
        }

        public override void PostDestroy(DestroyMode mode, Map previousMap)
        {
            for (int i = 0; i < _seedlingBlueprints.Count; ++i)
            {
                _seedlingBlueprints[i].Destroy();
            }
        }

        public AcceptanceReport CanSowAt(IntVec3 c, Map map)
        {
            if (!c.IsValid)
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

        public void Notify_DespawnBlueprint(Blueprint_PlantSeed bp)
        {
            _seedlingBlueprints.Remove(bp);
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
                        GenSpawn.WipeExistingThings(cell, Rot4.South, VVThingDefOf.VV_ArcanePlantSeedling.blueprintDef, parent.MapHeld, DestroyMode.Deconstruct);

                        var bp = (Blueprint_PlantSeed)ThingMaker.MakeThing(VVThingDefOf.VV_ArcanePlantSeedling.blueprintDef);
                        bp.SetSeedToPlant(parent);
                        bp.SetFactionDirect(Faction.OfPlayerSilentFail);
                        GenSpawn.Spawn(bp, target.Cell, parent.Map);

                        _seedlingBlueprints.Add(bp);

                        SoundDefOf.Tick_High.PlayOneShotOnCamera();

                        if (_seedlingBlueprints.Count < parent.stackCount)
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
