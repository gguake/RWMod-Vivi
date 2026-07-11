using RimWorld;
using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace VVRace
{
    [StaticConstructorOnStartup]
    public class CompVivi : ThingComp
    {
        public static Material AttunementLineMat = MaterialPool.MatFrom(GenDraw.LineTexPath, ShaderDatabase.Transparent, new Color(0.5f, 1f, 0.5f));
        public static Material ConcentrationLineMat = MaterialPool.MatFrom(GenDraw.LineTexPath, ShaderDatabase.Transparent, new Color(1f, 0.75f, 0.4f, 0.6f));

        public bool isRoyal = false;
        private Dictionary<GeneDef, int> _geneInheritanceGenerations = new Dictionary<GeneDef, int>();
        private Color? _originalHairColor = null;
        [Unsaved]
        private bool _refreshEverflowerHediffsAfterLoad;

        public ArcanePlant_Everflower LinkedEverflower => _linkedEverflower;
        private ArcanePlant_Everflower _linkedEverflower;
        private CompViviHolder ViviHolder => parent.GetComp<CompViviHolder>();

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
            Scribe_Collections.Look(ref _geneInheritanceGenerations, "geneInheritanceGenerations", LookMode.Def, LookMode.Value);

            if (_geneInheritanceGenerations == null)
            {
                _geneInheritanceGenerations = new Dictionary<GeneDef, int>();
            }

            Scribe_References.Look(ref _linkedEverflower, "linkedEverflower");

            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                _refreshEverflowerHediffsAfterLoad = true;
            }
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
            if (respawningAfterLoad)
            {
                _refreshEverflowerHediffsAfterLoad = true;
                TryRefreshEverflowerHediffsAfterLoad();
            }
            else
            {
                RefreshEverflowerHediffs();
            }
        }

        public override void PostDeSpawn(Map map, DestroyMode mode = DestroyMode.Vanish)
        {
            RefreshEverflowerHediffs();
        }

        public override void PostDestroy(DestroyMode mode, Map previousMap)
        {
            base.PostDestroy(mode, previousMap);

            if (LinkedEverflower != null)
            {
                LinkedEverflower.EverflowerComp.UnlinkAttunement((Pawn)parent, showMessages: false);
            }
        }

        public override void Notify_Killed(Map prevMap, DamageInfo? dinfo = null)
        {
            if (LinkedEverflower != null)
            {
                LinkedEverflower.EverflowerComp.UnlinkAttunement((Pawn)parent, showMessages: false);
            }
        }

        public override void CompTickInterval(int delta)
        {
            if (_refreshEverflowerHediffsAfterLoad)
            {
                TryRefreshEverflowerHediffsAfterLoad();
            }

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

        private bool TryRefreshEverflowerHediffsAfterLoad()
        {
            if (!parent.Spawned)
            {
                return false;
            }

            if (LinkedEverflower != null && !LinkedEverflower.Destroyed && !LinkedEverflower.Spawned)
            {
                return false;
            }

            _refreshEverflowerHediffsAfterLoad = false;
            RefreshEverflowerHediffs();
            return true;
        }

        public override void PostDrawExtraSelectionOverlays()
        {
            if (AttunementActive)
            {
                var a = parent.TrueCenter();
                var b = LinkedEverflower.TrueCenter();
                GenDraw.DrawLineBetween(a, b, AttunementLineMat);
            }

            if (isRoyal && parent.Spawned)
            {
                var marker = Hediff_FairyConcentrated.GetTargetOwnedBy((Pawn)parent);
                if (marker?.pawn != null && marker.pawn.Spawned && marker.pawn.Map == parent.Map)
                {
                    GenDraw.DrawLineBetween(((Pawn)parent).DrawPos, marker.pawn.DrawPos, ConcentrationLineMat);
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
                _geneInheritanceGenerations = new Dictionary<GeneDef, int>(sourceComp._geneInheritanceGenerations);
            }
        }

        public int GetGeneInheritanceGeneration(GeneDef geneDef)
        {
            if (geneDef != null && _geneInheritanceGenerations.TryGetValue(geneDef, out var generation))
            {
                return Mathf.Clamp(generation, 1, 3);
            }

            return 1;
        }

        public void SetGeneInheritanceGenerations(Dictionary<GeneDef, int> generations)
        {
            _geneInheritanceGenerations.Clear();
            if (generations == null) { return; }

            foreach (var pair in generations)
            {
                if (pair.Key != null)
                {
                    _geneInheritanceGenerations[pair.Key] = Mathf.Clamp(pair.Value, 1, 3);
                }
            }
        }

        public void ResetGeneInheritanceGenerations()
        {
            _geneInheritanceGenerations.Clear();
        }

        public void SetRoyal()
        {
            isRoyal = true;

            var pawn = (Pawn)parent;
            if (!pawn.health.hediffSet.HasHediff(VVHediffDefOf.VV_RoyalVivi))
            {
                pawn.health.AddHediff(VVHediffDefOf.VV_RoyalVivi);
            }

            RefreshEverflowerHediffs();
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

            RefreshEverflowerHediffs();
        }

        public void Notify_UnlinkEverflower(bool showMessages = true)
        {
            _linkedEverflower = null;

            RefreshEverflowerHediffs();

            if (showMessages)
            {
                Messages.Message(LocalizeString_Message.VV_Message_LinkDisconnectedEverflower.Translate(parent.Named("PAWN")), MessageTypeDefOf.NegativeEvent);
            }
        }

        public void RefreshEverflowerHediffs()
        {
            var pawn = (Pawn)parent;
            if (pawn.health == null) { return; }

            RefreshEverflowerLinkHediff(pawn);
            RefreshFairyMasteryHediff(pawn);
        }

        private void RefreshEverflowerLinkHediff(Pawn pawn)
        {
            var hediff = pawn.health.hediffSet.GetFirstHediffOfDef(VVHediffDefOf.VV_EverflowerLink);
            if (AttunementActive)
            {
                if (hediff == null)
                {
                    hediff = pawn.health.AddHediff(VVHediffDefOf.VV_EverflowerLink);
                }
                hediff.Severity = LinkedEverflower.EverflowerComp.AttunementHediffSeverity;
                return;
            }

            if (hediff != null)
            {
                pawn.health.RemoveHediff(hediff);
            }
        }

        private void RefreshFairyMasteryHediff(Pawn pawn)
        {
            var hediff = pawn.health.hediffSet.GetFirstHediffOfDef(VVHediffDefOf.VV_FairyMastery);
            if (AttunementActive && LinkedEverflower?.EverflowerComp.AttunementLevel >= 4)
            {
                if (hediff == null)
                {
                    pawn.health.AddHediff(VVHediffDefOf.VV_FairyMastery);
                }
                return;
            }

            if (hediff != null)
            {
                pawn.health.RemoveHediff(hediff);
            }

            var holder = ViviHolder;
            if (holder != null && (hediff != null || holder.MaterializedCount > 0 || holder.PendingMaterializeCount > 0))
            {
                holder.DematerializeAll();
            }
        }
    }
}
