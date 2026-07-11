using RimWorld;
using System.Collections.Generic;
using Verse;

namespace VVRace
{
    public class Recipe_ApplyArchiteGermlineImprint : Recipe_Surgery
    {
        public override bool AvailableOnNow(Thing thing, BodyPartRecord part = null)
        {
            if (!base.AvailableOnNow(thing, part) || !(thing is Pawn pawn))
            {
                return false;
            }

            return pawn.IsRoyalVivi() &&
                !pawn.health.hediffSet.HasHediff(VVHediffDefOf.VV_ArchiteGermlineImprint);
        }

        public override void ApplyOnPawn(
            Pawn pawn,
            BodyPartRecord part,
            Pawn billDoer,
            List<Thing> ingredients,
            Bill bill)
        {
            if (CheckSurgeryFail(billDoer, pawn, ingredients, part, bill))
            {
                return;
            }

            if (pawn.IsRoyalVivi() &&
                !pawn.health.hediffSet.HasHediff(VVHediffDefOf.VV_ArchiteGermlineImprint))
            {
                pawn.health.AddHediff(VVHediffDefOf.VV_ArchiteGermlineImprint);
            }
        }
    }
}
