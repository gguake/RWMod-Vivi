﻿using RimWorld;
using UnityEngine;
using Verse;

namespace VVRace
{
    public class CompProperties_Vivi : CompProperties
    {
        public CompProperties_Vivi()
        {
            compClass = typeof(CompVivi);
        }
    }

    public class CompVivi : ThingComp
    {
        public bool isRoyal = false;
        private Color? _originalHairColor = null;

        public bool ShouldBeRoyalIfMature
        {
            get
            {
                var need = ((Pawn)parent).needs.TryGetNeed<Need_RoyalJelly>();
                if (need == null) { return false; }

                return need.ShouldBeRoyalIfMature;
            }
        }

        public override void PostExposeData()
        {
            Scribe_Values.Look(ref isRoyal, "isRoyal");
            Scribe_Values.Look(ref _originalHairColor, "originalHairColor");
        }

        public override void PostSpawnSetup(bool respawningAfterLoad)
        {
            RefreshHairColor();
        }

        public override void CompTick()
        {
            if (parent.IsHashIntervalTick(60000))
            {
                RefreshHairColor();
            }
        }

        public void SetRoyal()
        {
            isRoyal = true;

        }

        public void RefreshHairColor()
        {
            var pawn = (Pawn)parent;
            if (_originalHairColor == null)
            {
                _originalHairColor = new Color(pawn.story.HairColor.r, pawn.story.HairColor.g, pawn.story.HairColor.b, 1f);
            }

            if (!pawn.DevelopmentalStage.Adult())
            {
                var appliedHairColor = Color.Lerp(Color.white, _originalHairColor.Value, (float)pawn.ageTracker.AgeBiologicalTicks / pawn.ageTracker.AdultMinAgeTicks);
                pawn.story.HairColor = appliedHairColor;

                pawn.Drawer.renderer.graphics.SetAllGraphicsDirty();
            }
        }


        public void Notify_ChildLifeStageStart()
        {
            RefreshHairColor();
        }

        public void Notify_AdultLifeStageStart()
        {
            var pawn = parent as Pawn;
            if (_originalHairColor != null)
            {
                pawn.story.HairColor = _originalHairColor.Value;
                _originalHairColor = null;
            }

            if (ShouldBeRoyalIfMature)
            {
                SetRoyal();

                pawn.story.bodyType = BodyTypeDefOf.Female;
                pawn.apparel?.DropAllOrMoveAllToInventory((Apparel apparel) => !apparel.def.apparel.PawnCanWear(pawn));

                if (pawn.IsColonist)
                {
                    Find.LetterStack.ReceiveLetter(
                        LocalizeTexts.LetterRoyalViviGrownLabel.Translate(this.Named("PAWN")),
                        LocalizeTexts.LetterRoyalViviGrown.Translate(this.Named("PAWN")),
                        LetterDefOf.PositiveEvent,
                        pawn);
                }
            }
            else
            {
                pawn.story.bodyType = BodyTypeDefOf.Thin;
                pawn.apparel?.DropAllOrMoveAllToInventory((Apparel apparel) => !apparel.def.apparel.PawnCanWear(pawn));
            }

            pawn.Drawer.renderer.graphics.SetAllGraphicsDirty();
        }
    }
}
