using RimWorld;
using System.Collections.Generic;
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
            if (respawningAfterLoad)
            {
                var pawn = (Pawn)parent;
                if (isRoyal && !pawn.health.hediffSet.HasHediff(VVHediffDefOf.VV_RoyalVivi))
                {
                    pawn.health.AddHediff(VVHediffDefOf.VV_RoyalVivi);
                }

                if (pawn.story.bodyType == BodyTypeDefOf.Thin && pawn.DevelopmentalStage.Juvenile())
                {
                    if (pawn.DevelopmentalStage.Child())
                    {
                        pawn.story.bodyType = BodyTypeDefOf.Child;
                    }
                    else if (pawn.DevelopmentalStage.Baby())
                    {
                        pawn.story.bodyType = BodyTypeDefOf.Baby;
                    }
                }
            }

            RefreshHairColor();
        }

        public override void PostDeSpawn(Map map)
        {
            base.PostDeSpawn(map);
        }

        public override void CompTick()
        {
            if (parent.IsHashIntervalTick(60000))
            {
                RefreshHairColor();
            }
        }

        public override void Notify_DuplicatedFrom(Pawn source)
        {
            var sourceComp = source.GetComp<CompVivi>();
            if (sourceComp != null)
            {
                isRoyal = sourceComp.isRoyal;
                _originalHairColor = sourceComp._originalHairColor;
            }
        }

        public void SetRoyal()
        {
            isRoyal = true;

            var pawn = (Pawn)parent;
            if (!pawn.health.hediffSet.HasHediff(VVHediffDefOf.VV_RoyalVivi))
            {
                pawn.health.AddHediff(VVHediffDefOf.VV_RoyalVivi);
            }
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

                pawn.Drawer.renderer.SetAllGraphicsDirty();
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

            if (!pawn.IsRoyalVivi())
            {
                if (ShouldBeRoyalIfMature)
                {
                    SetRoyal();

                    pawn.story.bodyType = BodyTypeDefOf.Female;
                    pawn.apparel?.DropAllOrMoveAllToInventory((Apparel apparel) => !apparel.def.apparel.PawnCanWear(pawn));

                    if (pawn.IsColonist)
                    {
                        Find.LetterStack.ReceiveLetter(
                            LocalizeString_Letter.VV_Letter_RoyalViviGrownLabel.Translate(pawn.Named("PAWN")),
                            LocalizeString_Letter.VV_Letter_RoyalViviGrown.Translate(pawn.Named("PAWN")),
                            LetterDefOf.PositiveEvent,
                            pawn);
                    }
                }
                else
                {
                    pawn.story.bodyType = BodyTypeDefOf.Thin;
                    pawn.apparel?.DropAllOrMoveAllToInventory((Apparel apparel) => !apparel.def.apparel.PawnCanWear(pawn));
                }

                pawn.Drawer.renderer.SetAllGraphicsDirty();
            }
        }
    }
}
