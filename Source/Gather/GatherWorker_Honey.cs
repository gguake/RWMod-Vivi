using HarmonyLib;
using MonoMod.Utils;
using RimWorld;
using System;
using System.Linq;
using Verse;

namespace VVRace
{
    public class GatherWorker_Honey : GatherWorker_Plant
    {
        private delegate void Pawn_FilthTracker_DropCarriedFilth_Delegate(Pawn_FilthTracker self, Filth f);
        private static Pawn_FilthTracker_DropCarriedFilth_Delegate Pawn_FilthTracker_DropCarriedFilth =
            AccessTools.Method(typeof(Pawn_FilthTracker), "DropCarriedFilth").CreateDelegate<Pawn_FilthTracker_DropCarriedFilth_Delegate>();

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

                if (Rand.Chance(0.05f))
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
                    Pawn_FilthTracker_DropCarriedFilth(pawn.filth, filth);
                }
            }
        }
    }
}
