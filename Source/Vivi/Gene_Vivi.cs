using RimWorld;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;

namespace VVRace
{
    public class Gene_Vivi : Gene
    {
        public GeneDefExt Def => (GeneDefExt)def;

        public bool IsRoyal
        {
            get
            {
                return isRoyal;
            }
            private set
            {
                isRoyal = value;

                if (isRoyal)
                {
                    if (eggSettings == null)
                    {
                        eggSettings = new ViviEggSettings(this);
                    }

                    if (mindLinkSettings != null)
                    {
                        mindLinkSettings = null;
                    }
                }
                else
                {
                    if (eggSettings != null)
                    {
                        eggSettings = null;
                    }

                    if (mindLinkSettings == null)
                    {
                        mindLinkSettings = new ViviMindLinkSettings(this);
                    }
                }
            }
        }

        public ViviEggSettings ViviEggSettings => eggSettings;
        public ViviMindLinkSettings ViviMindLinkSettings => mindLinkSettings;

        private bool shouldCheckApparels;

        private bool isRoyal;
        private ViviEggSettings eggSettings;
        private ViviMindLinkSettings mindLinkSettings;
        private Color? originalHairColor;

        public override string Label => isRoyal ? 
            string.Concat(LocalizeTexts.RoyalGeneLabelPrefix.Translate(), " ", base.Label) :
            base.Label;

        public override void ExposeData()
        {
            base.ExposeData();

            Scribe_Values.Look(ref isRoyal, "isRoyal");
            Scribe_Deep.Look(ref eggSettings, "eggSettings", this);
            Scribe_Deep.Look(ref mindLinkSettings, "mindLinkSettings", this);
            Scribe_Values.Look(ref originalHairColor, "originalHairColor");

            if (Scribe.mode == LoadSaveMode.ResolvingCrossRefs)
            {
                eggSettings?.ResolveReferences(this);
                mindLinkSettings?.ResolveReferences(this);
            }
        }

        public override void Tick()
        {
            base.Tick();

            if (shouldCheckApparels)
            {
                pawn.apparel?.DropAllOrMoveAllToInventory((Apparel apparel) => !apparel.def.apparel.PawnCanWear(pawn));
                shouldCheckApparels = false;
            }

            if (pawn.IsHashIntervalTick(2500))
            {
                eggSettings?.Tick(2500);
            }

            if (pawn.IsHashIntervalTick(15000) && originalHairColor.HasValue && pawn.DevelopmentalStage.Child())
            {
                var ageOffset = pawn.ageTracker.AgeBiologicalYearsFloat / pawn.ageTracker.AdultMinAge;
                var hairColorOffset = Mathf.Clamp(ageOffset * ageOffset, 0, 1);
                pawn.story.HairColor = Color.Lerp(Color.white, originalHairColor.Value, hairColorOffset);
                pawn.Drawer.renderer.graphics.SetAllGraphicsDirty();
            }
        }

        public override IEnumerable<Gizmo> GetGizmos()
        {
            if (!pawn.IsColonistPlayerControlled)
            {
                yield break;
            }

            if (!IsRoyal)
            {
                if (mindLinkSettings != null)
                {
                    foreach (var gizmo in mindLinkSettings.GetGizmos())
                    {
                        yield return gizmo;
                    }
                }
            }
            else
            {
                if (eggSettings != null)
                {
                    foreach (var gizmo in eggSettings.GetGizmos())
                    {
                        yield return gizmo;
                    }
                }
            }
        }

        public override void PostAdd()
        {
            base.PostAdd();

            var kindDefExt = pawn.kindDef as PawnKindDefExt;
            IsRoyal = kindDefExt != null && kindDefExt.isRoyal;

            if (IsRoyal)
            {
                IsRoyal = true;

                if (!pawn.health.hediffSet.HasHediff(VVHediffDefOf.VV_MindTransmitter))
                {
                    pawn.health.AddHediff(VVHediffDefOf.VV_MindTransmitter);
                }

                // 로얄 비비인 경우는 마인드 링크를 제거한다.
                if (pawn.TryGetMindLink(out var mindLink) && mindLink.linker != null && mindLink.linker.TryGetMindTransmitter(out var parentMindTransmitter))
                {
                    parentMindTransmitter.UnassignPawnControl(pawn);
                }

                if (!kindDefExt.preventRoyalBodyType)
                {
                    pawn.genes.AddGene(VVGeneDefOf.Body_Standard, true);
                    pawn.story.bodyType = BodyTypeDefOf.Female;

                    var shouldRemoveApparels = pawn.apparel.WornApparel.Where((Apparel apparel) => !apparel.def.apparel.PawnCanWear(pawn)).ToList();
                    foreach (var apparel in shouldRemoveApparels)
                    {
                        pawn.apparel.Remove(apparel);
                    }

                    pawn.Drawer.renderer.graphics.SetAllGraphicsDirty();
                }
            }
        }

        public bool ShouldBeRoyalIfMature()
        {
            var need = pawn.needs.TryGetNeed<Need_RoyalJelly>();
            if (need == null) { return false; }

            return need.ShouldBeRoyalIfMature;
        }

        #region Notificaitons
        public void Notify_MakeNewMindLink(Pawn linker)
        {
            if (!pawn.health?.hediffSet?.HasHediff(VVHediffDefOf.VV_MindLink) ?? false)
            {
                var hediff = pawn.health.AddHediff(VVHediffDefOf.VV_MindLink) as Hediff_MindLink;
                hediff.linker = linker;
            }

            mindLinkSettings?.Notify_MindLinkConnected();
        }

        public void Notify_RemoveMindLink(bool directlyRemoveHediff)
        {
            var hediff = pawn.health?.hediffSet?.GetFirstHediffOfDef(VVHediffDefOf.VV_MindLink) as Hediff_MindLink;
            if (hediff != null)
            {
                if (directlyRemoveHediff)
                {
                    pawn.health.RemoveHediff(hediff);
                }
                else
                {
                    hediff.linker = null;
                }
            }
        }

        public void Notify_ForceBreakingMindLink()
        {
            if (mindLinkSettings?.TryGetHediffMindLink(out var hediffMindLink) != true) { return; }

            var linkElapsedTicks = hediffMindLink.ConnectedTicks;
            if (linkElapsedTicks > 0)
            {
                var penaltyStage = Mathf.Clamp(linkElapsedTicks / (12 * 60000), 0, 5) - 1;
                if (penaltyStage >= 0)
                {
                    var thought_Memory = ThoughtMaker.MakeThought(VVThoughtDefOf.VV_MindLinkForceBreaked, penaltyStage);
                    pawn.needs.mood.thoughts.memories.TryGainMemory(thought_Memory);
                }

                pawn.health.AddHediff(VVHediffDefOf.VV_PsychicConfusion, pawn.health.hediffSet.GetNotMissingParts().FirstOrDefault(b => b.def == BodyPartDefOf.Head));

                if (pawn.Drafted)
                {
                    pawn.drafter.Drafted = false;
                }

                pawn.InterruptCurrentJob();

                if (pawn.IsColonist)
                {
                    Messages.Message(LocalizeTexts.MessageMindLinkForceDisconnected.Translate(pawn.Named("LINKED")),
                        new LookTargets(new Thing[] { pawn }),
                        MessageTypeDefOf.NegativeEvent);
                }
            }
        }

        public void Notify_ChildLifeStageStart()
        {
            originalHairColor = pawn.story.HairColor;
            pawn.story.HairColor = Color.white;
            pawn.Drawer.renderer.graphics.SetAllGraphicsDirty();
        }

        public void Notify_AdultLifeStageStarted()
        {
            if (originalHairColor.HasValue)
            {
                pawn.story.HairColor = originalHairColor.Value;
                originalHairColor = null;
            }

            if (ShouldBeRoyalIfMature())
            {
                IsRoyal = true;

                if (!pawn.health.hediffSet.HasHediff(VVHediffDefOf.VV_MindTransmitter))
                {
                    pawn.health.AddHediff(VVHediffDefOf.VV_MindTransmitter);
                }

                pawn.genes.AddGene(VVGeneDefOf.Body_Standard, true);
                pawn.story.bodyType = BodyTypeDefOf.Female;
                pawn.apparel?.DropAllOrMoveAllToInventory((Apparel apparel) => !apparel.def.apparel.PawnCanWear(pawn));

                pawn.Drawer.renderer.graphics.SetAllGraphicsDirty();

                if (pawn.IsColonist)
                {
                    Find.LetterStack.ReceiveLetter(
                        LocalizeTexts.LetterRoyalViviGrownLabel.Translate(pawn.Named("PAWN")),
                        LocalizeTexts.LetterRoyalViviGrown.Translate(pawn.Named("PAWN")),
                        LetterDefOf.PositiveEvent,
                        pawn);
                }
            }
            else
            {
                pawn.story.bodyType = BodyTypeDefOf.Thin;
                pawn.apparel?.DropAllOrMoveAllToInventory((Apparel apparel) => !apparel.def.apparel.PawnCanWear(pawn));

                pawn.Drawer.renderer.graphics.SetAllGraphicsDirty();
            }
        }

        public void Notify_PregnantHediffAdded(Hediff_Pregnant hediff)
        {
            var list = hediff.Father?.genes?.Endogenes?.Select(gene => gene.def).ToList();
            if (list.NullOrEmpty()) { return; }

            var count = Mathf.Clamp(Rand.RangeInclusive(1, 5), 1, list.Count());
            list.Shuffle();

            if (eggSettings != null)
            {
                for (int i = 0; i < count; ++i)
                {
                    eggSettings.StoreGene(list[i]);
                }
            }
        }
        #endregion


    }
}
