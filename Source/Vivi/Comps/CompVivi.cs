using RimWorld;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using Verse;

namespace VVRace
{
    [StaticConstructorOnStartup]
    public class CompVivi : ThingComp
    {
        public static Material AttunementLineMat = MaterialPool.MatFrom(GenDraw.LineTexPath, ShaderDatabase.Transparent, new Color(0.5f, 1f, 0.5f));
        public static Material ConcentrationLineMat = MaterialPool.MatFrom(GenDraw.LineTexPath, ShaderDatabase.Transparent, new Color(1f, 0.75f, 0.4f, 0.6f));

        private const float HairColorChangeTolerance = 0.002f;

        public bool isRoyal = false;
        private Dictionary<GeneDef, int> _geneInheritanceGenerations = new Dictionary<GeneDef, int>();
        // 어린 비비 머리색 변화: 자연 머리색, 마지막으로 칠한 색, 바깥(염색 등)에서 한 번이라도 바뀌었는지
        private Color? _originalHairColor = null;
        private Color? _appliedHairColor = null;
        private bool _hairColorOverridden = false;
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

        public float MetabolismFoodConsumptionFactor
        {
            get
            {
                var pawn = parent as Pawn;
                if (!ModsConfig.BiotechActive || pawn?.genes == null)
                {
                    return 1f;
                }

                var metabolism = 0;
                foreach (var gene in pawn.genes.GenesListForReading)
                {
                    if (!gene.Overridden)
                    {
                        metabolism += gene.def.biostatMet;
                    }
                }

                return Mathf.Max(1f, GeneTuning.MetabolismToFoodConsumptionFactorCurve.Evaluate(metabolism));
            }
        }

        public override float GetStatFactor(StatDef stat)
        {
            return stat == StatDefOf.MaxNutrition ? MetabolismFoodConsumptionFactor : 1f;
        }

        public override void GetStatsExplanation(StatDef stat, StringBuilder sb, string whitespace = "")
        {
            base.GetStatsExplanation(stat, sb, whitespace);

            if (stat == StatDefOf.MaxNutrition && MetabolismFoodConsumptionFactor > 1f)
            {
                sb.AppendLine(whitespace + LocalizeString_Stat.VV_StatReport_ViviMetabolismMaxNutrition.Translate(
                    MetabolismFoodConsumptionFactor.ToStringPercent()));
            }
        }

        public override void PostExposeData()
        {
            Scribe_Values.Look(ref isRoyal, "isRoyal");
            Scribe_Values.Look(ref _originalHairColor, "originalHairColor");
            Scribe_Values.Look(ref _appliedHairColor, "appliedHairColor");
            Scribe_Values.Look(ref _hairColorOverridden, "hairColorOverridden");
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

                if (parent.IsHashIntervalTick(EverflowerBlessingUtility.RefreshIntervalTicks, delta))
                {
                    EverflowerBlessingUtility.RefreshFor((Pawn)parent);
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

        public override IEnumerable<Gizmo> CompGetGizmosExtra()
        {
            foreach (var gizmo in base.CompGetGizmosExtra())
            {
                yield return gizmo;
            }

            if (!DebugSettings.godMode || !(parent is Pawn pawn) || !pawn.Spawned)
            {
                yield break;
            }

            yield return new Command_Action
            {
                defaultLabel = "DEV: Refresh everflower rewards",
                action = () => EverflowerBlessingUtility.RefreshFor(pawn)
            };

            yield return new Command_Action
            {
                defaultLabel = "DEV: Add genetic instability",
                action = () =>
                {
                    if (!pawn.health.hediffSet.HasHediff(VVHediffDefOf.VV_GeneticUnstability))
                    {
                        pawn.health.AddHediff(VVHediffDefOf.VV_GeneticUnstability, pawn.RaceProps.body.corePart);
                    }
                }
            };

            if (!isRoyal && pawn.DevelopmentalStage.Adult())
            {
                yield return new Command_Action
                {
                    defaultLabel = "DEV: Coronate",
                    action = () => Coronate()
                };
            }

            if (!pawn.health.hediffSet.HasHediff(VVHediffDefOf.VV_EverflowerQueen))
            {
                yield return new Command_Action
                {
                    defaultLabel = "DEV: Grant queen powers",
                    action = () => ViviEndingUtility.GrantQueenPowers(pawn, ViviEndingQuest.Current?.AnchorEverflower ?? LinkedEverflower)
                };
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
                _appliedHairColor = sourceComp._appliedHairColor;
                _hairColorOverridden = sourceComp._hairColorOverridden;
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

        public bool Coronate()
        {
            var pawn = (Pawn)parent;
            if (isRoyal || !pawn.DevelopmentalStage.Adult()) { return false; }

            SetRoyal();

            pawn.story.bodyType = BodyTypeDefOf.Female;
            pawn.apparel?.DropAllOrMoveAllToInventory((Apparel apparel) => !apparel.def.apparel.PawnCanWear(pawn));
            pawn.Drawer.renderer.SetAllGraphicsDirty();
            return true;
        }

        public void RefreshHairColor()
        {
            var pawn = (Pawn)parent;
            // 폰 생성 중(나이 설정이 어린이 단계 시작을 알림)에는 머리색 유전자가 아직 붙기 전이라 자연 머리색을 잘못 잡으므로 맵에 있을 때만 다룬다.
            if (!pawn.Spawned || pawn.DevelopmentalStage.Adult() || CheckHairColorOverridden(pawn)) { return; }

            if (_originalHairColor == null)
            {
                var hairColor = pawn.story.HairColor;
                _originalHairColor = new Color(hairColor.r, hairColor.g, hairColor.b, 1f);
            }

            pawn.story.HairColor = Color.Lerp(Color.white, _originalHairColor.Value, (float)pawn.ageTracker.AgeBiologicalTicks / pawn.ageTracker.AdultMinAgeTicks);
            _appliedHairColor = pawn.story.HairColor;

            pawn.Drawer.renderer.SetAllGraphicsDirty();
        }

        // 마지막으로 칠한 색과 지금 색이 다르면 염색·유전자 등 다른 사유로 바뀐 것이므로 머리색 변화를 영구히 끈다.
        private bool CheckHairColorOverridden(Pawn pawn)
        {
            if (_hairColorOverridden) { return true; }

            var hairColor = pawn.story.HairColor;
            if (_appliedHairColor.HasValue)
            {
                if (IsSameHairColor(hairColor, _appliedHairColor.Value)) { return false; }
            }
            else
            {
                // 칠한 색을 기록하기 전 세이브는 막 염색을 마친 상태(원하는 색 == 지금 색)만 바뀐 것으로 본다.
                var nextHairColor = pawn.style?.nextHairColor;
                if (_originalHairColor == null || !nextHairColor.HasValue || !IsSameHairColor(hairColor, nextHairColor.Value)) { return false; }
            }

            _hairColorOverridden = true;
            _originalHairColor = null;
            _appliedHairColor = null;
            return true;
        }

        // 세이브 때 색 값이 반올림될 수 있어 작은 오차는 같은 색으로 본다.
        private static bool IsSameHairColor(Color a, Color b)
        {
            return Mathf.Abs(a.r - b.r) <= HairColorChangeTolerance
                && Mathf.Abs(a.g - b.g) <= HairColorChangeTolerance
                && Mathf.Abs(a.b - b.b) <= HairColorChangeTolerance;
        }

        public void Notify_ChildLifeStageStart()
        {
            RefreshHairColor();
        }

        public void Notify_AdultLifeStageStart()
        {
            var pawn = parent as Pawn;
            if (!CheckHairColorOverridden(pawn) && _originalHairColor != null)
            {
                pawn.story.HairColor = _originalHairColor.Value;
            }
            _originalHairColor = null;
            _appliedHairColor = null;

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
            RefreshFairyBondHediff(pawn);
        }

        // RefreshBondHediff로 구버전 세이브에 남은 결속 수치 오차를 보정한다.
        // 6단계 영원꽃과의 연결로 효과 단계도 바뀔 수 있으므로 스탯 캐시를 갱신한다.
        private void RefreshFairyBondHediff(Pawn pawn)
        {
            if (pawn.Dead) { return; }

            ViviHolder?.RefreshBondHediff();

            var bond = pawn.health.hediffSet.GetFirstHediffOfDef(VVHediffDefOf.VV_ViviFairyFollow);
            if (bond != null)
            {
                pawn.health.Notify_HediffChanged(bond);
            }
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
