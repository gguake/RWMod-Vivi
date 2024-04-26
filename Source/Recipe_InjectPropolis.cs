using RimWorld;
using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace VVRace
{
    public class Recipe_InjectPropolis : Recipe_Surgery
    {
        public const float BaseOffset = 0.1f;

        public override void ApplyOnPawn(Pawn pawn, BodyPartRecord part, Pawn billDoer, List<Thing> ingredients, Bill bill)
        {
            List<Hediff> targetHediffs = new List<Hediff>();
            pawn.health.hediffSet.GetHediffs(ref targetHediffs, hediff =>
            {
                if (hediff.def == HediffDefOf.WoundInfection ||
                    hediff.def == VVHediffDefOf.Flu ||
                    hediff.def == HediffDefOf.Plague ||
                    hediff.def == VVHediffDefOf.Malaria)
                {
                    return true;
                }

                return false;
            });

            foreach (var hediff in targetHediffs)
            {
                var offset = BaseOffset;
                offset = Mathf.Clamp(offset * pawn.GetStatValue(StatDefOf.ImmunityGainSpeed), 0.01f, 1f);
                hediff.Severity = Mathf.Max(0.01f, hediff.Severity - offset);
            }
        }
    }
}
