using RimWorld;
using UnityEngine;
using Verse;

namespace VVRace
{
    [StaticConstructorOnStartup]
    public class CompVivi : ThingComp
    {
        public static Material AttunementLineMat = MaterialPool.MatFrom(GenDraw.LineTexPath, ShaderDatabase.Transparent, new Color(0.5f, 1f, 0.5f));

        public bool isRoyal = false;
        private Color? _originalHairColor = null;

        public ArcanePlant_Everflower LinkedEverflower => _linkedEverflower;
        private ArcanePlant_Everflower _linkedEverflower;

        public bool ShouldBeRoyalIfMature
        {
            get
            {
                var need = ((Pawn)parent).needs.TryGetNeed<Need_RoyalJelly>();
                if (need == null) { return false; }

                return need.ShouldBeRoyalIfMature;
            }
        }

        public bool AttunementActive
        {
            get
            {
                return isRoyal && parent.Spawned && LinkedEverflower != null && LinkedEverflower.Spawned && parent.Map == LinkedEverflower.Map;
            }
        }

        public override void PostExposeData()
        {
            Scribe_Values.Look(ref isRoyal, "isRoyal");
            Scribe_Values.Look(ref _originalHairColor, "originalHairColor");

            Scribe_References.Look(ref _linkedEverflower, "linkedEverflower");
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
            else
            {
                if (AttunementActive)
                {
                    GiveEverflowerLinkHediff();
                }
            }

            RefreshHairColor();
        }

        public override void PostDeSpawn(Map map, DestroyMode mode = DestroyMode.Vanish)
        {
            RemoveEverflowerLinkHediff();
        }

        public override void PostDestroy(DestroyMode mode, Map previousMap)
        {
            base.PostDestroy(mode, previousMap);

            if (LinkedEverflower != null)
            {
                LinkedEverflower.EverflowerComp.UnlinkAttunement((Pawn)parent);
            }
        }

        public override void Notify_Killed(Map prevMap, DamageInfo? dinfo = null)
        {
            if (LinkedEverflower != null)
            {
                LinkedEverflower.EverflowerComp.UnlinkAttunement((Pawn)parent);
            }
        }

        public override void CompTickInterval(int delta)
        {
            if (parent.Spawned)
            {
                if (parent.IsHashIntervalTick(20000, delta))
                {
                    RefreshHairColor();
                }

                if (AttunementActive && parent.IsHashIntervalTick(GenTicks.TickRareInterval, delta))
                {
                    var mapManaComp = parent.Map.GetManaComponent();
                    var mana = mapManaComp[parent.Position];

                    LinkedEverflower.EverflowerComp.GainAttunementFromMana(mana);
                }
            }
        }

        public override void PostDrawExtraSelectionOverlays()
        {
            if (AttunementActive)
            {
                var a = parent.TrueCenter();
                var b = LinkedEverflower.TrueCenter();
                GenDraw.DrawLineBetween(a, b, AttunementLineMat);
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

        public void Notify_LinkEverflower(ArcanePlant_Everflower everflower)
        {
            _linkedEverflower = everflower;

            Find.LetterStack.ReceiveLetter(
                LocalizeString_Letter.VV_Letter_LinkEverflowerLabel.Translate(),
                LocalizeString_Letter.VV_Letter_LinkEverflower.Translate(parent.Named("PAWN")),
                LetterDefOf.PositiveEvent,
                parent);

            GiveEverflowerLinkHediff();
        }

        public void Notify_LinkedEverflowerDestroyed()
        {
            _linkedEverflower = null;

            RemoveEverflowerLinkHediff();
            Messages.Message(LocalizeString_Message.VV_Message_LinkDisconnectedEverflower.Translate(parent.Named("PAWN")), MessageTypeDefOf.NegativeEvent);
        }

        private void GiveEverflowerLinkHediff()
        {
            var pawn = (Pawn)parent;
            var hediff = pawn.health.hediffSet.GetFirstHediffOfDef(VVHediffDefOf.VV_EverflowerLink);
            if (hediff == null)
            {
                hediff = pawn.health.AddHediff(VVHediffDefOf.VV_EverflowerLink);
                hediff.Severity = LinkedEverflower.EverflowerComp.AttunementHediffSeverity;
            }
        }

        private void RemoveEverflowerLinkHediff()
        {
            var pawn = (Pawn)parent;
            var hediff = pawn.health.hediffSet.GetFirstHediffOfDef(VVHediffDefOf.VV_EverflowerLink);
            if (hediff != null)
            {
                pawn.health.RemoveHediff(hediff);
            }
        }
    }
}
