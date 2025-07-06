using HarmonyLib;
using RimWorld;
using System;
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
    }

    public class GatherWorker_Honey : GatherWorker_Plant
    {
        public override void Notify_Gathered(Pawn pawn, Thing billGiver, Thing target, RecipeDef_Gathering recipe)
        {
            base.Notify_Gathered(pawn, billGiver, target, recipe);

            if (target is Plant plant)
            {
                if (pawn.filth != null && Rand.Bool)
                {
                    pawn.filth.GainFilth(VVThingDefOf.VV_FilthPollen, Gen.YieldSingle(target.def.defName));
                }
                else if (Rand.Bool)
                {
                    FilthMaker.TryMakeFilth(pawn.Position, pawn.Map, VVThingDefOf.VV_FilthPollen, target.def.defName, 1);
                }

                if (Rand.Chance(0.04f))
                {
                    //var seed = ThingMaker.MakeThing(VVThingDefOf.VV_UnknownSeed);
                    //seed.stackCount = 1;

                    //GenPlace.TryPlaceThing(seed, pawn.Position, pawn.Map, ThingPlaceMode.Near);
                }
            }
        }

        public override void Notify_ProcessStarted(Pawn pawn)
        {
            base.Notify_ProcessStarted(pawn);

            var filths = pawn.filth.CarriedFilthListForReading;
            for (int i = 0; i < filths.Count; ++i)
            {
                var filth = filths[i];
                if (filth.def == VVThingDefOf.VV_FilthPollen && filth.CanDropAt(pawn.Position, pawn.Map))
                {
                    Pawn_FilthTracker_ReversePatch.DropCarriedFilth(pawn.filth, filth);
                }
            }
        }

        public override void Notify_RecipeComplete(Pawn pawn, ThingDef productDef, ref float productCount)
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
        }
    }
}
