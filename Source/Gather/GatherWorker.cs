﻿using RimWorld;
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

        public abstract IEnumerable<Thing> FindAllGatherableTargetInRegion(Region region);

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
                    var beforeHitPoint = target.HitPoints;
                    var afterHitPoint = Mathf.Max(1, (int)(target.HitPoints * Rand.Range(0.8f, 0.9f)));
                    var damage = beforeHitPoint - afterHitPoint;
                    if (damage > 0)
                    {
                        MoteMaker.ThrowText((pawn.DrawPos + target.DrawPos) / 2f, pawn.Map, LocalizeString_Etc.VV_TextMote_GatherFailed.Translate(), 3.65f);
                        target.TakeDamage(new DamageInfo(DamageDefOf.Crush, damage));
                    }
                }
            }

            if (target is IGatherableTarget notifyReceiver)
            {
                notifyReceiver.Notify_Gathered(pawn, recipe);
            }
        }

        public virtual void Notify_ProcessStarted(Pawn pawn)
        {
        }

        public virtual void Notify_RecipeComplete(Pawn pawn, ThingDef productDef, ref float productCount)
        {
        }
    }
}
