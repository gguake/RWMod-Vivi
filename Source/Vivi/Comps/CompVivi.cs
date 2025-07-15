using RimWorld;
using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace VVRace
{
    public class CompProperties_Vivi : CompProperties
    {
        public SimpleCurve everflowerAttuneRateCurve;
        public SimpleCurve everflowerAttuneLevelCurve;

        public CompProperties_Vivi()
        {
            compClass = typeof(CompVivi);
        }
    }

    public class CompVivi : ThingComp
    {
        public CompProperties_Vivi Props => (CompProperties_Vivi)props;

        public bool isRoyal = false;
        private Color? _originalHairColor = null;

        public ArcanePlant_Everflower LinkedEverflower => _linkedEverflower;
        private ArcanePlant_Everflower _linkedEverflower;

        public float EverflowerAttunement
        {
            get => _everflowerAttunement;
            set
            {
                var minimumAttunement = Props.everflowerAttuneLevelCurve.EvaluateInverted(_everflowerAttunementLevel);
                _everflowerAttunement = Mathf.Max(minimumAttunement, value);
                
                var afterLevel = (int)Props.everflowerAttuneLevelCurve.Evaluate(_everflowerAttunement);
                if (afterLevel > _everflowerAttunementLevel)
                {
                    AttunementLevelUp(afterLevel);
                }

                UpdateLinkHediff();
            }
        }

        private int _everflowerAttunementLevel;
        private float _everflowerAttunement;

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

            Scribe_References.Look(ref _linkedEverflower, "linkedEverflower");
            Scribe_Values.Look(ref _everflowerAttunementLevel, "_everflowerAttunementLevel");
            Scribe_Values.Look(ref _everflowerAttunement, "everflowerAttunement");
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
                if (isRoyal && LinkedEverflower != null && LinkedEverflower.Spawned && LinkedEverflower.Map == parent.Map)
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

        public override void CompTickInterval(int delta)
        {
            if (parent.Spawned)
            {
                if (parent.IsHashIntervalTick(20000, delta))
                {
                    RefreshHairColor();
                }

                if (isRoyal && parent.IsHashIntervalTick(GenTicks.TickRareInterval, delta) && LinkedEverflower != null)
                {
                    var mapManaComp = parent.Map.GetManaComponent();
                    var mana = mapManaComp[parent.Position];

                    EverflowerAttunement += Props.everflowerAttuneRateCurve.Evaluate(mana) / 60000f * GenTicks.TickRareInterval * delta;
                }
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

        public override IEnumerable<Gizmo> CompGetGizmosExtra()
        {
            if (DebugSettings.godMode)
            {
                if (isRoyal && LinkedEverflower != null)
                {
                    yield return new Command_Action()
                    {
                        defaultLabel = "DEV: +10 Attunement EXP",
                        action = () =>
                        {
                            EverflowerAttunement += 10;
                        }
                    };
                }
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
            _everflowerAttunementLevel = Mathf.Max(1, _everflowerAttunementLevel);

            Find.LetterStack.ReceiveLetter(
                LocalizeString_Letter.VV_Letter_LinkEverflowerLabel.Translate(),
                LocalizeString_Letter.VV_Letter_LinkEverflower.Translate(parent.Named("PAWN")),
                LetterDefOf.PositiveEvent,
                parent);

            GiveEverflowerLinkHediff();
        }

        public void Notify_LinkedEverflowerSpawned()
        {
            if (isRoyal && LinkedEverflower != null && LinkedEverflower.Spawned && LinkedEverflower.Map == parent.Map)
            {
                GiveEverflowerLinkHediff();
            }
        }

        public void Notify_LinkedEverflowerDespawned()
        {
            RemoveEverflowerLinkHediff();
        }

        public void Notify_LinkedEverflowerDestroyed()
        {
            _linkedEverflower = null;
            EverflowerAttunement = EverflowerAttunement * 0.7f;

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
            }

            UpdateLinkHediff(hediff);
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

        private void AttunementLevelUp(int level)
        {
            _everflowerAttunementLevel = level;

            Find.LetterStack.ReceiveLetter(
                LocalizeString_Letter.VV_Letter_EverflowerAttumentLevelUpLabel.Translate(parent.Named("PAWN"), level.Named("LEVEL")),
                LocalizeString_Letter.VV_Letter_EverflowerAttumentLevelUp.Translate(parent.Named("PAWN"), level.Named("LEVEL")),
                LetterDefOf.PositiveEvent,
                parent);
        }

        private void UpdateLinkHediff(Hediff hediff = null)
        {
            if (hediff == null)
            {
                hediff = ((Pawn)parent).health.hediffSet.GetFirstHediffOfDef(VVHediffDefOf.VV_EverflowerLink);
            }

            var nextLevelExp = Props.everflowerAttuneLevelCurve.EvaluateInverted(_everflowerAttunementLevel + 1);
            var curLevelExp = Props.everflowerAttuneLevelCurve.EvaluateInverted(_everflowerAttunementLevel);
            hediff.Severity = _everflowerAttunementLevel + (_everflowerAttunement - curLevelExp) / (nextLevelExp - curLevelExp);
        }
    }
}
