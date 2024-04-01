using HarmonyLib;
using MonoMod.Utils;
using RimWorld;
using System;
using System.Linq;
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

                if (Rand.Chance(0.07f))
                {
                    var seed = ThingMaker.MakeThing(VVThingDefOf.VV_UnknownSeed);
                    seed.stackCount = 1;

                    GenPlace.TryPlaceThing(seed, pawn.Position, pawn.Map, ThingPlaceMode.Near);
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
    }
}
