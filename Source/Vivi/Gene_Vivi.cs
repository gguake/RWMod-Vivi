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
                }
                else
                {
                    if (eggSettings != null)
                    {
                        eggSettings = null;
                    }
                }
            }
        }

        public ViviEggSettings ViviEggSettings => eggSettings;

        public ViviControlSettings ViviControlSettings => controlSettings;
        public Pawn MindLinkWishPawn => controlSettings == null ? mindLinkWishPawn : null;
        public bool MindLinkUnassignRequested => controlSettings == null ? false : controlSettings.UnassignRequested;

        private bool shouldCheckApparels;

        private bool isRoyal;
        private ViviEggSettings eggSettings;
        private ViviControlSettings controlSettings;
        private Pawn mindLinkWishPawn;
        private Color? originalHairColor;

        #region overrides
        public override string Label => isRoyal ? 
            string.Concat(LocalizeTexts.RoyalGeneLabelPrefix.Translate(), " ", base.Label) :
            base.Label;

        public override void ExposeData()
        {
            base.ExposeData();

            Scribe_Values.Look(ref isRoyal, "isRoyal");
            Scribe_Deep.Look(ref eggSettings, "eggSettings", this);
            Scribe_Deep.Look(ref controlSettings, "controlSettings", this);
            Scribe_References.Look(ref mindLinkWishPawn, "mindLinkWishPawn");
            Scribe_Values.Look(ref originalHairColor, "originalHairColor");

            if (Scribe.mode == LoadSaveMode.ResolvingCrossRefs)
            {
                if (eggSettings != null) { eggSettings.gene = this; }
                if (controlSettings != null) { controlSettings.gene = this; }
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
            if (pawn.IsColonistPlayerControlled)
            {
                if (!IsRoyal && pawn.CanMakeNewMindLink())
                {
                    if (controlSettings == null)
                    {
                        var command_mindLink = new Command_Toggle();
                        command_mindLink.isActive = () => mindLinkWishPawn != null;

                        if (mindLinkWishPawn != null)
                        {
                            command_mindLink.toggleAction = delegate
                            {
                                mindLinkWishPawn = null;
                            };
                        }
                        else
                        {
                            var candidates = pawn.Map.mapPawns.FreeColonistsSpawned.Where(p => p != pawn && p.HasMindTransmitter() && !p.Dead && p.Spawned).ToList();
                            command_mindLink.disabled = !candidates.Any();
                            command_mindLink.toggleAction = delegate
                            {
                                Find.WindowStack.Add(new FloatMenu(candidates.Select(pawn =>
                                {
                                    pawn.TryGetMindTransmitter(out var mindTransmitter);
                                    return new FloatMenuOption($"{pawn.Name.ToStringShort} ({mindTransmitter?.UsedBandwidth} / {mindTransmitter?.TotalBandWidth})", delegate
                                    {
                                        mindLinkWishPawn = pawn;
                                    });

                                }).ToList()));
                            };
                        }

                        command_mindLink.icon = TexCommand.HoldOpen;
                        command_mindLink.turnOnSound = SoundDefOf.Checkbox_TurnedOn;
                        command_mindLink.turnOffSound = SoundDefOf.Checkbox_TurnedOff;
                        command_mindLink.defaultLabel = (mindLinkWishPawn != null ? LocalizeTexts.CommandCancelConnectMindLink : LocalizeTexts.CommandConnectMindLink).Translate();
                        command_mindLink.defaultDesc = LocalizeTexts.CommandConnectMindLinkDesc.Translate();
                        yield return command_mindLink;
                    }
                    else
                    {
                        var command_mindLink = new Command_Toggle();
                        command_mindLink.isActive = () => controlSettings.UnassignRequested;
                        command_mindLink.toggleAction = delegate
                        {
                            controlSettings.UnassignRequested = !controlSettings.UnassignRequested;
                        };
                        command_mindLink.icon = TexCommand.SelectCarriedPawn;
                        command_mindLink.turnOnSound = SoundDefOf.Checkbox_TurnedOn;
                        command_mindLink.turnOffSound = SoundDefOf.Checkbox_TurnedOff;
                        command_mindLink.defaultLabel = (controlSettings.UnassignRequested ? LocalizeTexts.CommandCancelDisconnectMindLink : LocalizeTexts.CommandDisconnectMindLink).Translate();
                        command_mindLink.defaultDesc = LocalizeTexts.CommandDisconnectMindLinkDesc.Translate();
                        yield return command_mindLink;
                    }
                }
                else
                {
                    if (eggSettings != null)
                    {
                        yield return new EggProgressGizmo(eggSettings);

                        if (DebugSettings.godMode)
                        {
                            Command_Action command_addEggProgress = new Command_Action();
                            command_addEggProgress.defaultLabel = "DEV: Add Egg Progress";
                            command_addEggProgress.action = () =>
                            {
                                eggSettings.AddEggProgressDirectlyForDebug(0.1f);
                            };
                            yield return command_addEggProgress;
                        }
                    }
                }
            }
        }

        public override void PostAdd()
        {
            base.PostAdd();

            if (pawn.kindDef is PawnKindDefExt kindDefExt && kindDefExt.isRoyal)
            {
                MakeRoyal(kindDefExt.preventRoyalBodyType);
            }
        }
        #endregion

        public bool ShouldBeRoyalIfMature()
        {
            var need = pawn.needs.TryGetNeed<Need_RoyalJelly>();
            if (need == null) { return false; }

            return need.ShouldBeRoyalIfMature;
        }

        public void MakeRoyal(bool preventChangeBodyType = false)
        {
            IsRoyal = true;

            if (!pawn.health.hediffSet.HasHediff(VVHediffDefOf.VV_MindTransmitter))
            {
                pawn.health.AddHediff(VVHediffDefOf.VV_MindTransmitter);
            }

            if (!preventChangeBodyType)
            {
                pawn.genes.AddGene(VVGeneDefOf.Body_Standard, true);
                pawn.story.bodyType = BodyTypeDefOf.Female;

                var shouldRemoveApparels = new List<Apparel>();
                foreach (var apparel in pawn.apparel.WornApparel)
                {
                    if (!apparel.def.apparel.PawnCanWear(pawn))
                    {
                        shouldRemoveApparels.Add(apparel);
                    }
                }

                foreach (var apparel in  shouldRemoveApparels)
                {
                    pawn.apparel.Remove(apparel);
                }


                pawn.Drawer.renderer.graphics.SetAllGraphicsDirty();
            }
        }

        #region Notificaitons
        public void Notify_MakeNewMindLink()
        {
            mindLinkWishPawn = null;

            controlSettings = new ViviControlSettings(this);
            controlSettings.Notify_NewMindLink();

            if (!pawn.health?.hediffSet?.HasHediff(VVHediffDefOf.VV_MindLink) ?? false)
            {
                pawn.health.AddHediff(VVHediffDefOf.VV_MindLink);
            }
        }

        public void Notify_RemoveMindLink()
        {
            var mindLinkHediff = pawn.health?.hediffSet?.GetFirstHediffOfDef(VVHediffDefOf.VV_MindLink);
            if (mindLinkHediff != null)
            {
                pawn.health.RemoveHediff(mindLinkHediff);
            }

            controlSettings = null;
        }

        public void Notify_ForceBreakingMindLink()
        {
            if (controlSettings == null) { return; }

            var linkElapsedTicks = controlSettings.MindLinkElapsedTicks;
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

                Messages.Message(LocalizeTexts.MessageMindLinkForceDisconnected.Translate(pawn.Named("LINKED")),
                    new LookTargets(new Thing[] { pawn }),
                    MessageTypeDefOf.NegativeEvent);
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
                // 성장 후 로얄 비비가 된 경우는 마인드 링크를 해제한다.
                var directRelations = pawn.relations?.DirectRelations?.Where(v => v.def == VVPawnRelationDefOf.VV_MindLink).ToList();
                foreach (var relation in directRelations)
                {
                    pawn.relations.RemoveDirectRelation(relation);
                }

                IsRoyal = true;

                if (!pawn.health.hediffSet.HasHediff(VVHediffDefOf.VV_MindTransmitter))
                {
                    pawn.health.AddHediff(VVHediffDefOf.VV_MindTransmitter);
                }

                pawn.genes.AddGene(VVGeneDefOf.Body_Standard, true);
                pawn.story.bodyType = BodyTypeDefOf.Female;
                pawn.apparel?.DropAllOrMoveAllToInventory((Apparel apparel) => !apparel.def.apparel.PawnCanWear(pawn));

                pawn.Drawer.renderer.graphics.SetAllGraphicsDirty();
                Find.LetterStack.ReceiveLetter(
                    LocalizeTexts.LetterRoyalViviGrownLabel.Translate(pawn.Named("PAWN")),
                    LocalizeTexts.LetterRoyalViviGrown.Translate(pawn.Named("PAWN")),
                    LetterDefOf.PositiveEvent,
                    pawn);
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

        public void Notify_MindLinkWishPawnDead()
        {
            mindLinkWishPawn = null;
        }
        #endregion


    }
}
