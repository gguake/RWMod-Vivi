using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using UnityEngine;
using Verse;
using Verse.AI;
using VVRace.Honey;

namespace VVRace
{
    public class JobDriver_HarvestHoney : JobDriver
    {
        protected const TargetIndex PlantTargetIndex = TargetIndex.A;
        protected const TargetIndex HarvesterBuildingTargetIndex = TargetIndex.B;
        protected const TargetIndex StorageCellTargetIndex = TargetIndex.C;

        protected LocalTargetInfo PlantTargetInfo => job.GetTarget(PlantTargetIndex);
        protected LocalTargetInfo HarvesterBuildingTargetInfo => job.GetTarget(HarvesterBuildingTargetIndex);

        protected Plant Plant => PlantTargetInfo.Thing as Plant;
        protected Thing HarvesterBuilding => HarvesterBuildingTargetInfo.Thing;

        private int TotalWorkAmount => (int)job.bill.recipe.workAmount;
        private int HarvestWorkAmount => Mathf.CeilToInt(TotalWorkAmount * 0.85f);
        private int ProcessWorkAmount => Mathf.CeilToInt(TotalWorkAmount * 0.15f);

        private bool IsBillDisabled
        {
            get
            {
                if (job.bill.DeletedOrDereferenced)
                {
                    return true;
                }

                if (job.bill.suspended)
                {
                    return true;
                }

                if (!(HarvesterBuilding is IBillGiver billGiver)) { return true; }
                if (!billGiver.CurrentlyUsableForBills())
                {
                    return true;
                }

                return false;
            }
        }

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            return pawn.Reserve(TargetA, job, errorOnFailed: errorOnFailed);
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            // 식물로 이동
            yield return Toils_Goto.GotoThing(PlantTargetIndex, PathEndMode.Touch)
                .FailOnDespawnedNullOrForbidden(PlantTargetIndex)
                .FailOnBurningImmobile(PlantTargetIndex)
                .FailOn(() => IsBillDisabled);

            // 식물에서 꿀을 수확
            var harvestWorkAmount = Mathf.CeilToInt(HarvestWorkAmount / (job.RecipeDef.efficiencyStat != null ? pawn.GetStatValue(job.RecipeDef.efficiencyStat) : 1f));
            yield return Toils_General.Wait(harvestWorkAmount, PlantTargetIndex)
                .FailOnDespawnedNullOrForbidden(PlantTargetIndex)
                .FailOnBurningImmobile(PlantTargetIndex)
                .FailOn(() => IsBillDisabled)
                .WithInitAction(() =>
                {
                    job.bill.Notify_BillWorkStarted(pawn);
                })
                .WithFailCondition(() => !Plant.CanGatherable(VVStatDefOf.VV_PlantHoneyGatherYield, VVStatDefOf.VV_PlantGatherCooldown) || Plant.Blighted)
                .WithEffect(() => GetActor().CurJob.bill.recipe.effectWorking, TargetIndex.A)
                .WithProgressBarToilDelay(PlantTargetIndex);

            // 꽃가루 생성 및 쿨타임 설정
            yield return new Toil()
                .WithDefaultCompleteMode(ToilCompleteMode.Instant)
                .WithInitAction(() =>
                {
                    if (Plant is Plant plant)
                    {
                        var compGatherable = plant.GetComp<CompGatherable>();
                        compGatherable.Gathered();

                        if (pawn.filth != null && Rand.Bool)
                        {
                            pawn.filth.GainFilth(VVThingDefOf.VV_FilthPollen, Gen.YieldSingle(Plant.def.defName));
                        }

                        if (Rand.Bool)
                        {
                            FilthMaker.TryMakeFilth(pawn.Position, pawn.Map, VVThingDefOf.VV_FilthPollen, Plant.def.defName, 1);
                        }
                    }
                });

            // 작업대로 이동
            yield return Toils_Goto.GotoThing(HarvesterBuildingTargetIndex, PathEndMode.InteractionCell)
                .FailOnDespawnedNullOrForbidden(HarvesterBuildingTargetIndex)
                .FailOnBurningImmobile(HarvesterBuildingTargetIndex)
                .FailOn(() => IsBillDisabled);

            // 작업대에서 꿀을 가공
            var processWorkAmount = Mathf.CeilToInt(ProcessWorkAmount / pawn.GetStatValue(StatDefOf.WorkSpeedGlobal));
            yield return Toils_General.Wait(processWorkAmount, HarvesterBuildingTargetIndex)
                .FailOnDespawnedNullOrForbidden(HarvesterBuildingTargetIndex)
                .FailOnBurningImmobile(HarvesterBuildingTargetIndex)
                .FailOn(() => IsBillDisabled)
                .WithInitAction(() =>
                {
                    pawn.rotationTracker.FaceTarget(HarvesterBuilding);
                })
                .WithTickAction(() =>
                {
                    job.bill.Notify_PawnDidWork(pawn);
                    if (HarvesterBuilding is IBillGiverWithTickAction billGiverWithTickAction)
                    {
                        billGiverWithTickAction.UsedThisTick();
                    }
                })
                .WithHandlingFacing()
                .WithProgressBarToilDelay(HarvesterBuildingTargetIndex, interpolateBetweenActorAndTarget: true)
                .WithActiveSkill(() => job.bill.recipe.workSkill)
                .FailOnCannotTouch(HarvesterBuildingTargetIndex, PathEndMode.InteractionCell)
                .PlaySustainerOrSound(() => GetActor().CurJob.bill.recipe.soundWorking);

            // 작업대에서 꿀을 저장
            yield return new Toil()
                .WithDefaultCompleteMode(ToilCompleteMode.Instant)
                .WithInitAction(() =>
                {
                    var actor = GetActor();
                    var curJob = actor.jobs.curJob;
                    var billGiver = HarvesterBuilding;

                    job.bill.Notify_BillWorkFinished(pawn);

                    // 스킬 레벨업 처리
                    if (curJob.RecipeDef.workSkill != null && !curJob.RecipeDef.UsesUnfinishedThing)
                    {
                        float xp = job.bill.recipe.workAmount * 0.1f * curJob.RecipeDef.workSkillLearnFactor;
                        actor.skills.GetSkill(curJob.RecipeDef.workSkill).Learn(xp);
                    }

                    // 스탯 및 작업대 효율 보정 계산
                    var efficiency = curJob.RecipeDef.efficiencyStat != null ? actor.GetStatValue(curJob.RecipeDef.efficiencyStat) : 1f;
                    if (curJob.RecipeDef.workTableEfficiencyStat != null)
                    {
                        if (HarvesterBuilding is Building_WorkTable building_WorkTable)
                        {
                            efficiency *= building_WorkTable.GetStatValue(curJob.RecipeDef.workTableEfficiencyStat);
                        }
                    }

                    var allProducts = new List<Thing>();
                    foreach (var productThingDefCount in curJob.RecipeDef.products)
                    {
                        var productCount = productThingDefCount.count * efficiency;

                        while (productCount > 0)
                        {
                            var stackCount = Mathf.CeilToInt(Mathf.Clamp(productCount, 1f, productThingDefCount.thingDef.stackLimit));
                            productCount -= stackCount;

                            var product = ThingMaker.MakeThing(productThingDefCount.thingDef);
                            product.stackCount = stackCount;

                            allProducts.Add(product);
                        }
                    }

                    // thing 생성 후 notification
                    curJob.bill.Notify_IterationCompleted(actor, new List<Thing>());
                    RecordsUtility.Notify_BillDone(actor, allProducts);

                    if (allProducts.Count > 0)
                    {
                        Find.QuestManager.Notify_ThingsProduced(actor, allProducts);

                        if (curJob.bill.GetStoreMode() == BillStoreModeDefOf.DropOnFloor)
                        {
                            // 바닥에 놓는 경우
                            for (int i = 0; i < allProducts.Count; ++i)
                            {
                                if (!GenPlace.TryPlaceThing(allProducts[i], actor.Position, actor.Map, ThingPlaceMode.Near))
                                {
                                    Log.Error(string.Concat(actor, " could not drop recipe product ", allProducts[i], " near ", actor.Position));
                                }
                            }
                            actor.jobs.EndCurrentJob(JobCondition.Succeeded);
                        }
                        else
                        {
                            // 결과물이 여러개인 경우 맨 앞의 하나만 들고가고 나머지는 바닥에 둔다.
                            for (int i = 1; i < allProducts.Count; ++i)
                            {
                                if (!GenPlace.TryPlaceThing(allProducts[i], actor.Position, actor.Map, ThingPlaceMode.Near))
                                {
                                    Log.Error(string.Concat(actor, " could not drop recipe product ", allProducts[i], " near ", actor.Position));
                                }
                            }

                            IntVec3 foundCell = IntVec3.Invalid;
                            if (curJob.bill.GetStoreMode() == BillStoreModeDefOf.BestStockpile)
                            {
                                StoreUtility.TryFindBestBetterStoreCellFor(allProducts[0], actor, actor.Map, StoragePriority.Unstored, actor.Faction, out foundCell);
                            }
                            else if (curJob.bill.GetStoreMode() == BillStoreModeDefOf.SpecificStockpile)
                            {
                                StoreUtility.TryFindBestBetterStoreCellForIn(allProducts[0], actor, actor.Map, StoragePriority.Unstored, actor.Faction, curJob.bill.GetStoreZone().slotGroup, out foundCell);
                            }

                            if (foundCell.IsValid)
                            {
                                actor.carryTracker.TryStartCarry(allProducts[0]);
                                curJob.targetC = foundCell;
                                curJob.count = 99999;
                            }
                            else
                            {
                                if (!GenPlace.TryPlaceThing(allProducts[0], actor.Position, actor.Map, ThingPlaceMode.Near))
                                {
                                    Log.Error($"Bill doer could not drop product {allProducts[0]} near {actor.Position}");
                                }

                                actor.jobs.EndCurrentJob(JobCondition.Succeeded);
                            }
                        }
                    }
                });

            yield return Toils_Reserve.Reserve(StorageCellTargetIndex);
            yield return Toils_Haul.CarryHauledThingToCell(StorageCellTargetIndex);
            yield return Toils_Haul.PlaceHauledThingInCell(
                StorageCellTargetIndex,
                Toils_Haul.CarryHauledThingToCell(StorageCellTargetIndex),
                storageMode: true,
                tryStoreInSameStorageIfSpotCantHoldWholeStack: true);

            yield return new Toil()
                .WithInitAction(() =>
                {
                    var bill_Production = GetActor().jobs.curJob.bill as Bill_Production;
                    if (bill_Production != null && bill_Production.repeatMode == BillRepeatModeDefOf.TargetCount)
                    {
                        base.Map.resourceCounter.UpdateResourceCounts();
                    }
                });
        }
    }
}
