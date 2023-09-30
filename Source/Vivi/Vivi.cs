using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;
using Verse.AI;

namespace VVRace
{
    public class Vivi : Pawn
    {
        public bool IsRoyal => _isRoyal;

        public bool CanLayEgg => _isRoyal && _eggProgress >= 1f;
        public float EggProgress => _eggProgress;
        public float EggProgressPerDays => Mathf.Clamp01(PawnUtility.BodyResourceGrowthSpeed(this) / 30f);

        public bool ShouldBeRoyalIfMature
        {
            get
            {
                var need = needs.TryGetNeed<Need_RoyalJelly>();
                if (need == null) { return false; }

                return need.ShouldBeRoyalIfMature;
            }
        }

        private bool _isRoyal = false;

        private float _eggProgress = 0f;
        private Color? _originalHairColor = null;

        public override void ExposeData()
        {
            base.ExposeData();

            Scribe_Values.Look(ref _isRoyal, "isRoyal");

            Scribe_Values.Look(ref _eggProgress, "eggProgress");
            Scribe_Values.Look(ref _originalHairColor, "originalHairColor");
        }

        public override void Tick()
        {
            base.Tick();

            if (_isRoyal)
            {
                if (_eggProgress < 1f)
                {
                    _eggProgress = Mathf.Clamp01(_eggProgress + EggProgressPerDays / 60000f);
                }
            }

            if (this.IsHashIntervalTick(2000 * 6))
            {
                RefreshHairColor();
            }
        }

        public override void SpawnSetup(Map map, bool respawningAfterLoad)
        {
            base.SpawnSetup(map, respawningAfterLoad);

            RefreshHairColor();
        }

        public override IEnumerable<Gizmo> GetGizmos()
        {
            foreach (var gizmo in base.GetGizmos())
            {
                yield return gizmo;
            }

            if (Spawned && this.IsColonistPlayerControlled && _isRoyal)
            {
                yield return new EggProgressGizmo(this);

                if (DebugSettings.godMode)
                {
                    Command_Action command_addEggProgress = new Command_Action();
                    command_addEggProgress.defaultLabel = "DEV: Add Egg Progress";
                    command_addEggProgress.action = () =>
                    {
                        _eggProgress = Mathf.Clamp01(_eggProgress + 0.1f);
                    };

                    yield return command_addEggProgress;
                }
            }
        }

        public override void SetFaction(Faction newFaction, Pawn recruiter = null)
        {
            var oldFaction = Faction;

            base.SetFaction(newFaction, recruiter);
        }

        public void SetRoyal()
        {
            _isRoyal = true;
        }

        public Thing ProduceEgg()
        {
            if (!CanLayEgg) { return null; }

            try
            {
                var egg = ThingMaker.MakeThing(VVThingDefOf.VV_ViviEgg);
                var hatcher = egg.TryGetComp<CompViviHatcher>();
                hatcher.hatcheeParent = this;
                hatcher.parentXenogenes = genes.Xenogenes.Select(v => v.def).ToList();
                hatcher.parentXenogenes.RemoveAll(
                    v => v.endogeneCategory == EndogeneCategory.BodyType ||
                    v.endogeneCategory == EndogeneCategory.Melanin ||
                    v.endogeneCategory == EndogeneCategory.HairColor ||
                    v.endogeneCategory == EndogeneCategory.Head ||
                    v.endogeneCategory == EndogeneCategory.Jaw);

                return egg;
            }
            finally
            {
                _eggProgress = 0f;
            }
        }

        public void Notify_ChildLifeStageStart()
        {
            RefreshHairColor();
        }

        public void Notify_AdultLifeStageStart()
        {
            if (_originalHairColor != null)
            {
                story.HairColor = _originalHairColor.Value;
                _originalHairColor = null;

            }

            if (ShouldBeRoyalIfMature)
            {
                SetRoyal();

                story.bodyType = BodyTypeDefOf.Female;
                apparel?.DropAllOrMoveAllToInventory((Apparel apparel) => !apparel.def.apparel.PawnCanWear(this));

                if (IsColonist)
                {
                    Find.LetterStack.ReceiveLetter(
                        LocalizeTexts.LetterRoyalViviGrownLabel.Translate(this.Named("PAWN")),
                        LocalizeTexts.LetterRoyalViviGrown.Translate(this.Named("PAWN")),
                        LetterDefOf.PositiveEvent,
                        this);
                }
            }
            else
            {
                story.bodyType = BodyTypeDefOf.Thin;
                apparel?.DropAllOrMoveAllToInventory((Apparel apparel) => !apparel.def.apparel.PawnCanWear(this));
            }

            Drawer.renderer.graphics.SetAllGraphicsDirty();
        }

        private void RefreshHairColor()
        {
            if (_originalHairColor == null)
            {
                _originalHairColor = new Color(story.HairColor.r, story.HairColor.g, story.HairColor.b, 1f);
            }

            if (!DevelopmentalStage.Adult())
            {
                var appliedHairColor = Color.Lerp(Color.white, _originalHairColor.Value, (float)ageTracker.AgeBiologicalTicks / ageTracker.AdultMinAgeTicks);
                story.HairColor = appliedHairColor;

                Drawer.renderer.graphics.SetAllGraphicsDirty();
            }
        }
    }
}
