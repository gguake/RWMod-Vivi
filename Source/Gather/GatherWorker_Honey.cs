using HarmonyLib;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;

namespace VVRace
{
    [HarmonyPatch]
    public static class Pawn_FilthTracker_ReversePatch
    {
        [HarmonyReversePatch]
        [HarmonyPatch(typeof(Pawn_FilthTracker), "DropCarriedFilth")]
        public static void DropCarriedFilth(Pawn_FilthTracker __instance, Filth f)
        {
            throw new NotImplementedException("stub");
        }

        [HarmonyReversePatch]
        [HarmonyPatch(typeof(Pawn_FilthTracker), "ThinCarriedFilth")]
        public static void ThinCarriedFilth(Pawn_FilthTracker __instance, Filth f)
        {
            throw new NotImplementedException("stub");
        }
    }

    public class GatherWorker_Honey : GatherWorker_Plant
    {
        public override void Notify_Gathered(Pawn pawn, Thing billGiver, Thing target, RecipeDef_Gathering recipe)
        {
            base.Notify_Gathered(pawn, billGiver, target, recipe);

            if (target is Plant plant)
            {
                var pollenChance = recipe.basePollenChance;
                pollenChance *= pawn.GetStatValue(recipe.efficiencyStat) * target.GetStatValue(recipe.targetYieldStat);

                if (pollenChance > 0 && Rand.Chance(pollenChance))
                {
                    if (pawn.filth != null)
                    {
                        pawn.filth.GainFilth(VVThingDefOf.VV_FilthPollen);
                    }
                    else
                    {
                        FilthMaker.TryMakeFilth(pawn.Position, pawn.Map, VVThingDefOf.VV_FilthPollen, target.def.defName, 1);
                    }
                }
            }
        }

        public override void Notify_ProcessStarted(Pawn pawn, Building_GatherWorkTable workTable)
        {
            base.Notify_ProcessStarted(pawn, workTable);

        }

        private List<Filth_Pollen> _tmpFilthPollens = new List<Filth_Pollen>();
        public override void Notify_RecipeComplete(Pawn pawn, Building_GatherWorkTable workTable, ThingDef productDef, ref float productCount)
        {
            var gene = pawn.genes.GenesListForReading.FirstOrDefault(v => v is Gene_HoneyDependency) as Gene_HoneyDependency;
            if (gene == null) { return; }

            var hediff = gene.LinkedHediff;
            if (hediff == null) { return; }

            if (hediff.Severity > 0.8f && productCount > 1f)
            {
                gene.Reset();
                productCount = Mathf.Clamp(productCount - 1f, 1f, productDef.stackLimit);
            }

            _tmpFilthPollens.Clear();
            _tmpFilthPollens.AddRange(pawn.filth.CarriedFilthListForReading.OfType<Filth_Pollen>());

            if (_tmpFilthPollens.Count > 0)
            {
                if (workTable.CanGatherFilth)
                {
                    for (int i = 0; i < _tmpFilthPollens.Count; ++i)
                    {
                        var filthPollen = _tmpFilthPollens[i];
                        filthPollen.GatherPollen(workTable.Map, pawn);

                        Pawn_FilthTracker_ReversePatch.ThinCarriedFilth(pawn.filth, filthPollen);
                    }
                }
                else
                {
                    for (int i = 0; i < _tmpFilthPollens.Count; ++i)
                    {
                        var filthPollen = _tmpFilthPollens[i];
                        if (filthPollen.CanDropAt(pawn.Position, pawn.Map))
                        {
                            Pawn_FilthTracker_ReversePatch.DropCarriedFilth(pawn.filth, filthPollen);
                        }
                    }
                }
            }
        }
    }
}
