using RimWorld;
using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace VVRace
{
    public class Recipe_InjectPropolis : Recipe_Surgery
    {
        public override void ApplyOnPawn(Pawn pawn, BodyPartRecord part, Pawn billDoer, List<Thing> ingredients, Bill bill)
        {
            List<Hediff> targetHediffs = new List<Hediff>();
            pawn.health.hediffSet.GetHediffs(ref targetHediffs, hediff =>
            {
                if (hediff.def == HediffDefOf.WoundInfection ||
                    hediff.def == HediffDefOf.Flu ||
                    hediff.def == HediffDefOf.Plague ||
                    hediff.def == HediffDefOf.Malaria)
                {
                    return true;
                }

                return false;
            });

            foreach (var hediff in targetHediffs)
            {
                hediff.Severity = Mathf.Max(0.01f, hediff.Severity - 0.18f);
            }
        }
    }
}
