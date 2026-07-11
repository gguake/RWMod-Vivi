using RimWorld;
using RimWorld.Utility;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;
using Verse.AI;
using Verse.Grammar;

namespace VVRace
{
    public class CompProperties_PerfumeBottle : CompProperties
    {
        public int maxIngredients = 3;
        public int maxSprays = 6;
        public int reloadCost = 200;
        public int gatherTicks = 1500;
        public int reloadTicks = 300;
        public float arcaneWeightPerFlower = 0.35f;
        public float ordinaryFlowerWeightBonus = 0.15f;
        public ThingDef pollenDef;

        public CompProperties_PerfumeBottle()
        {
            compClass = typeof(CompPerfumeBottle);
        }
    }

    [StaticConstructorOnStartup]
    public class CompPerfumeBottle : ThingComp, IReloadableComp
    {
        private static readonly Texture2D CollectIcon = ContentFinder<Texture2D>.Get("Things/Mote/VV_GatherHoney");
        private static readonly Texture2D ReloadIcon = ContentFinder<Texture2D>.Get("Things/Item/VV_ViviDust");

        private List<ThingDef> ingredients = new List<ThingDef>();
        private int spraysRemaining;
        private int pollenLoaded;
        private bool charged = true;

        public CompProperties_PerfumeBottle Props => (CompProperties_PerfumeBottle)props;
        public IReadOnlyList<ThingDef> Ingredients => ingredients;
        public int SpraysRemaining => spraysRemaining;
        public int PollenLoaded => pollenLoaded;
        public bool Charged => charged;
        public bool IsComplete => ingredients.Count >= Props.maxIngredients && spraysRemaining > 0;
        public bool CanCollect => charged && ingredients.Count < Props.maxIngredients && spraysRemaining == 0;
        public bool NeedsRecharge => !charged && ingredients.Count == 0 && spraysRemaining == 0;
        public int MinPollenNeeded => Mathf.Max(0, Props.reloadCost - pollenLoaded);
        public Color BlendColor => PerfumeUtility.GetBlendColor(ingredients);

        public override string TransformLabel(string label)
        {
            if (ingredients.Count == 0)
            {
                return ResolveLabel("r_perfume_empty");
            }

            if (!IsComplete)
            {
                return ResolveLabel("r_perfume_blending");
            }

            var blendName = PerfumeUtility.GetBlendName(
                ingredients,
                Props.arcaneWeightPerFlower,
                Props.ordinaryFlowerWeightBonus);
            return ResolveLabel("r_perfume_complete", blendName);
        }

        private string ResolveLabel(string rootKeyword, string blendName = null)
        {
            var request = new GrammarRequest();
            request.Includes.Add(VVRulePackDefOf.VV_NamerPerfumeBottle);
            request.Rules.Add(new Rule_String("bottle_label", parent.def.label));
            if (!blendName.NullOrEmpty())
            {
                request.Rules.Add(new Rule_String("blend_name", blendName));
            }

            return GrammarResolver.Resolve(
                rootKeyword,
                request,
                "perfume bottle name",
                capitalizeFirstSentence: false);
        }

        public override void PostExposeData()
        {
            base.PostExposeData();

            Scribe_Collections.Look(ref ingredients, "perfumeIngredients", LookMode.Def);
            Scribe_Values.Look(ref spraysRemaining, "perfumeSpraysRemaining");
            Scribe_Values.Look(ref pollenLoaded, "perfumePollenLoaded");
            Scribe_Values.Look(ref charged, "perfumeCharged", true);

            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                ingredients = ingredients?.Where(def => def != null).Take(Props.maxIngredients).ToList() ?? new List<ThingDef>();
                spraysRemaining = Mathf.Clamp(spraysRemaining, 0, Props.maxSprays);
                pollenLoaded = Mathf.Clamp(pollenLoaded, 0, Props.reloadCost);

                if (ingredients.Count >= Props.maxIngredients && spraysRemaining == 0 && charged)
                {
                    spraysRemaining = Props.maxSprays;
                }

                NotifyBlendColorChanged();
            }
        }

        public override string CompInspectStringExtra()
        {
            return GetStatusText();
        }

        public override IEnumerable<Gizmo> CompGetGizmosExtra()
        {
            yield return new PerfumeGizmo(this);
        }

        public override IEnumerable<Gizmo> CompGetWornGizmosExtra()
        {
            yield return new PerfumeGizmo(this);

            var wearer = WearingPawn;
            if (wearer == null || wearer.Faction != Faction.OfPlayerSilentFail)
            {
                yield break;
            }

            if (Find.Selector.SelectedPawns.Count > 1)
            {
                yield break;
            }

            if (CanCollect)
            {
                yield return MakeCollectCommand(wearer);
            }

            if (NeedsRecharge)
            {
                yield return new Command_Action
                {
                    defaultLabel = LocalizeString_Perfume.VV_Command_ReloadPerfumeBottle.Translate(),
                    defaultDesc = LocalizeString_Perfume.VV_Command_ReloadPerfumeBottleDesc.Translate(Props.reloadCost, Props.pollenDef.LabelCap),
                    icon = ReloadIcon,
                    action = () => StartReloadJob(wearer)
                };
            }
        }

        public Pawn WearingPawn => (parent as Apparel)?.Wearer;

        public bool CanCollectFrom(Pawn collector, Thing target, out string reason)
        {
            reason = null;
            if (!CanCollect)
            {
                reason = LocalizeString_Perfume.VV_Perfume_CannotCollectState.Translate().Resolve();
                return false;
            }

            if (collector == null || WearingPawn != collector)
            {
                reason = LocalizeString_Perfume.VV_Perfume_MustBeWorn.Translate().Resolve();
                return false;
            }

            if (target == null || target.Destroyed || !target.Spawned || !PerfumeUtility.IsPerfumeFlower(target.def))
            {
                reason = LocalizeString_Perfume.VV_Perfume_InvalidFlower.Translate().Resolve();
                return false;
            }

            if (target is Plant plant && plant.Growth < target.GetStatValue(VVStatDefOf.VV_MinGrowthPlantGatherable))
            {
                reason = LocalizeString_Perfume.VV_Perfume_FlowerTooYoung.Translate().Resolve();
                return false;
            }

            if (target.IsForbidden(collector) || !collector.CanReserveAndReach(target, PathEndMode.Touch, Danger.Some))
            {
                reason = LocalizeString_Perfume.VV_Perfume_FlowerUnreachable.Translate().Resolve();
                return false;
            }

            return true;
        }

        public bool TryCollect(Thing flower)
        {
            if (!CanCollect || flower == null || !PerfumeUtility.IsPerfumeFlower(flower.def))
            {
                return false;
            }

            if (flower is Plant plant && plant.Growth < flower.GetStatValue(VVStatDefOf.VV_MinGrowthPlantGatherable))
            {
                return false;
            }

            ingredients.Add(flower.def);
            if (ingredients.Count >= Props.maxIngredients)
            {
                spraysRemaining = Props.maxSprays;
                NotifyBlendColorChanged();
            }

            return true;
        }

        public bool TrySpray(Pawn sprayer, float radius)
        {
            if (!IsComplete || sprayer == null || WearingPawn != sprayer || !sprayer.Spawned)
            {
                return false;
            }

            var blend = ingredients.ToList();
            foreach (var target in GenRadial.RadialDistinctThingsAround(sprayer.Position, sprayer.Map, radius, true).OfType<Pawn>())
            {
                if (target.RaceProps?.IsFlesh != true || target.health == null)
                {
                    continue;
                }

                var oldPerfumes = target.health.hediffSet.hediffs
                    .Where(hediff => hediff.def == VVHediffDefOf.VV_ArcanePerfume)
                    .ToList();
                foreach (var oldPerfume in oldPerfumes)
                {
                    target.health.RemoveHediff(oldPerfume);
                }

                var perfume = HediffMaker.MakeHediff(VVHediffDefOf.VV_ArcanePerfume, target) as Hediff_ArcanePerfume;
                if (perfume == null)
                {
                    continue;
                }

                perfume.SetBlend(blend, Props.arcaneWeightPerFlower, Props.ordinaryFlowerWeightBonus);
                target.health.AddHediff(perfume);
            }

            SpawnPerfumeEffect(sprayer);
            spraysRemaining--;
            if (spraysRemaining <= 0)
            {
                ingredients.Clear();
                spraysRemaining = 0;
                pollenLoaded = 0;
                charged = false;
                NotifyBlendColorChanged();
            }

            return true;
        }

        private void SpawnPerfumeEffect(Pawn sprayer)
        {
            var effecter = new Effecter(VVEffecterDefOf.VV_PerfumeSpray);
            foreach (var child in effecter.children)
            {
                child.colorOverride = BlendColor;
            }

            var target = new TargetInfo(sprayer);
            effecter.Trigger(target, target);
            effecter.Cleanup();
        }

        private void NotifyBlendColorChanged()
        {
            parent.Notify_ColorChanged();
            WearingPawn?.Drawer?.renderer?.SetAllGraphicsDirty();
        }

        public string GetStatusText()
        {
            if (NeedsRecharge)
            {
                return pollenLoaded > 0
                    ? LocalizeString_Perfume.VV_Perfume_StatusReloading.Translate(pollenLoaded, Props.reloadCost).Resolve()
                    : LocalizeString_Perfume.VV_Perfume_StatusNeedsReload.Translate(Props.reloadCost, Props.pollenDef.LabelCap).Resolve();
            }

            if (IsComplete)
            {
                return LocalizeString_Perfume.VV_Perfume_StatusSprays.Translate(spraysRemaining, Props.maxSprays).Resolve();
            }

            return LocalizeString_Perfume.VV_Perfume_StatusIngredients.Translate(ingredients.Count, Props.maxIngredients).Resolve();
        }

        public string GetTooltip()
        {
            var text = GetStatusText();
            if (ingredients.Count > 0)
            {
                text += "\n\n" + LocalizeString_Perfume.VV_Perfume_BlendHeader.Translate(
                    PerfumeUtility.GetBlendName(ingredients, Props.arcaneWeightPerFlower, Props.ordinaryFlowerWeightBonus));
                text += "\n\n" + PerfumeUtility.GetEffectDescription(
                    ingredients,
                    Props.arcaneWeightPerFlower,
                    Props.ordinaryFlowerWeightBonus);
            }

            return text;
        }

        private Command_Target MakeCollectCommand(Pawn wearer)
        {
            return new Command_Target
            {
                defaultLabel = LocalizeString_Perfume.VV_Command_CollectScent.Translate(),
                defaultDesc = LocalizeString_Perfume.VV_Command_CollectScentDesc.Translate(Props.maxIngredients - ingredients.Count),
                icon = CollectIcon,
                targetingParams = new TargetingParameters
                {
                    canTargetLocations = false,
                    canTargetPawns = false,
                    canTargetBuildings = true,
                    canTargetItems = false,
                    canTargetPlants = true,
                    validator = target => CanCollectFrom(wearer, target.Thing, out _)
                },
                action = target =>
                {
                    if (CanCollectFrom(wearer, target.Thing, out var reason))
                    {
                        var job = JobMaker.MakeJob(VVJobDefOf.VV_CollectPerfumeScent, target.Thing, parent);
                        wearer.jobs.TryTakeOrderedJob(job);
                    }
                    else if (!reason.NullOrEmpty())
                    {
                        Messages.Message(reason, MessageTypeDefOf.RejectInput, false);
                    }
                },
                onUpdate = target =>
                {
                    if (CanCollectFrom(wearer, target.Thing, out _))
                    {
                        GenDraw.DrawTargetHighlight(target);
                    }
                }
            };
        }

        private void StartReloadJob(Pawn wearer)
        {
            var ammo = ReloadableUtility.FindEnoughAmmo(wearer, wearer.Position, this, true);
            if (ammo.NullOrEmpty())
            {
                Messages.Message(
                    LocalizeString_Perfume.VV_Perfume_NotEnoughPollen.Translate(Props.reloadCost - pollenLoaded, Props.pollenDef.LabelCap),
                    MessageTypeDefOf.RejectInput,
                    false);
                return;
            }

            var job = JobMaker.MakeJob(VVJobDefOf.VV_ReloadPerfumeBottle, parent);
            job.targetQueueB = ammo.Select(thing => new LocalTargetInfo(thing)).ToList();
            job.count = Mathf.Min(ammo.Sum(thing => thing.stackCount), MinPollenNeeded);
            wearer.jobs.TryTakeOrderedJob(job);
        }

        public void ReloadFrom(Thing ammo)
        {
            if (!NeedsRecharge || ammo == null || ammo.def != Props.pollenDef)
            {
                return;
            }

            var amount = Mathf.Min(ammo.stackCount, MinPollenNeeded);
            if (amount <= 0)
            {
                return;
            }

            ammo.SplitOff(amount).Destroy();
            pollenLoaded += amount;
            if (pollenLoaded >= Props.reloadCost)
            {
                pollenLoaded = 0;
                charged = true;
            }
        }

        Thing IReloadableComp.ReloadableThing => parent;
        ThingDef IReloadableComp.AmmoDef => Props.pollenDef;
        int IReloadableComp.BaseReloadTicks => Props.reloadTicks;
        int IReloadableComp.MaxCharges => 1;
        int ICompWithCharges.RemainingCharges => charged ? 1 : 0;
        string IReloadableComp.LabelRemaining => GetStatusText();

        bool IReloadableComp.NeedsReload(bool allowForcedReload)
        {
            return NeedsRecharge;
        }

        int IReloadableComp.MinAmmoNeeded(bool allowForcedReload)
        {
            return MinPollenNeeded;
        }

        int IReloadableComp.MaxAmmoNeeded(bool allowForcedReload)
        {
            return MinPollenNeeded;
        }

        int IReloadableComp.MaxAmmoAmount()
        {
            return Props.reloadCost;
        }

        void IReloadableComp.ReloadFrom(Thing ammo)
        {
            ReloadFrom(ammo);
        }

        string IReloadableComp.DisabledReason(int minNeeded, int maxNeeded)
        {
            return LocalizeString_Perfume.VV_Perfume_NotEnoughPollen.Translate(
                MinPollenNeeded,
                Props.pollenDef.LabelCap).Resolve();
        }

        bool ICompWithCharges.CanBeUsed(out string reason)
        {
            reason = charged ? null : LocalizeString_Perfume.VV_Perfume_CannotCollectState.Translate().Resolve();
            return charged;
        }
    }
}
