using RimWorld;
using System;
using System.Collections.Generic;
using UnityEngine;
using Verse;
using Verse.AI;

namespace VVRace
{
    public interface IGatherableTarget
    {
        bool CanGatherByPawn(Pawn pawn, RecipeDef_Gathering recipe);

        void Notify_Gathered(Pawn pawn, RecipeDef_Gathering recipe);
    }

    public abstract class GatherWorker
    {
        public RecipeDef_Gathering recipeDef;

        public abstract string JobFailReasonIfNoHarvestable { get; }

        public virtual bool PawnCanDoBill(Pawn pawn, Bill bill)
        {
            return pawn.GetStatValue(bill.recipe.workSpeedStat) > 0f && pawn.GetStatValue(bill.recipe.efficiencyStat) > 0f;
        }

        public abstract Thing FilterGatherableTarget(Pawn pawn, Thing billGiver, Bill bill, IEnumerable<Thing> candidates);

        public virtual Job MakeJob(Pawn pawn, Thing billGiver, Thing target, Bill bill)
        {
            var recipeGathering = bill.recipe as RecipeDef_Gathering;

            var job = JobMaker.MakeJob(recipeGathering.gatheringJob, target, billGiver);
            job.bill = bill;
            job.haulMode = HaulMode.ToCellNonStorage;

            return job;
        }

        public virtual void Notify_Gathered(Pawn pawn, Thing billGiver, Thing target, RecipeDef_Gathering recipe)
        {
            if (recipe.targetCooldownStat != null)
            {
                if (target is ThingWithComps thingWithComps)
                {
                    var compGatherable = thingWithComps.GetComp<CompRepeatGatherable>();
                    if (compGatherable != null)
                    {
                        compGatherable.Gathered(recipe.targetCooldownStat);
                    }
                }
            }

            if (recipe.damageChanceBySkillLevel != null)
            {
                var damageChance = recipe.damageChanceBySkillLevel.Evaluate(pawn.skills.GetSkill(recipe.workSkill).Level);
                if (damageChance > 0 && Rand.Chance(damageChance))
                {
                    var damage = (int)(target.MaxHitPoints * Rand.Range(0.1f, 0.49f));
                    if (damage > 0)
                    {
                        MoteMaker.ThrowText((pawn.DrawPos + target.DrawPos) / 2f, pawn.Map, LocalizeString_Etc.VV_TextMote_GatherFailed.Translate(), 3.65f);
                        target.TakeDamage(new DamageInfo(DamageDefOf.SurgicalCut, damage));
                    }
                }
            }

            if (target is IGatherableTarget notifyReceiver)
            {
                notifyReceiver.Notify_Gathered(pawn, recipe);
            }

            if (target is Plant plant && recipe.baseArcaneSeedChance > 0)
            {
                if (Rand.Chance(recipe.baseArcaneSeedChance))
                {
                    var seed = ThingMaker.MakeThing(VVThingDefOf.VV_Seed_UnknownPlant);
                    seed.stackCount = 1;

                    GenPlace.TryPlaceThing(seed, pawn.Position, pawn.Map, ThingPlaceMode.Near);
                }
            }
        }

        public virtual void Notify_ProcessStarted(Pawn pawn, Building_GatherWorkTable workTable)
        {
        }

        public virtual void Notify_RecipeComplete(Pawn pawn, Building_GatherWorkTable workTable, ThingDef productDef, ref float productCount)
        {
        }
    }
}
