using RimWorld;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;
using Verse.AI;

namespace VVRace
{
    public class JobDriver_Gather : JobDriver
    {
        protected const TargetIndex GatherTargetIndex = TargetIndex.A;
        protected const TargetIndex BillGiverTargetIndex = TargetIndex.B;
        protected const TargetIndex StorageCellTargetIndex = TargetIndex.C;

        protected LocalTargetInfo GatherTargetInfo => job.GetTarget(GatherTargetIndex);
        protected LocalTargetInfo BillGiverTargetInfo => job.GetTarget(BillGiverTargetIndex);

        protected Thing BillGiver => BillGiverTargetInfo.Thing;

        private RecipeDef_Gathering GatheringRecipeDef => job.bill.recipe as RecipeDef_Gathering;

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

                if (!(BillGiver is IBillGiver billGiver)) { return true; }
                if (!billGiver.CurrentlyUsableForBills())
                {
                    return true;
                }

                return false;
            }
        }

        private List<float> gatherEfficiencyList = new List<float>();

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            return pawn.Reserve(TargetA, job, errorOnFailed: errorOnFailed);
        }

        public override void ExposeData()
        {
            base.ExposeData();

            Scribe_Collections.Look(ref gatherEfficiencyList, "gatherEfficiencyList", LookMode.Value);
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            // 이동
            yield return Toils_Goto.GotoThing(GatherTargetIndex, PathEndMode.Touch)
                .FailOnDespawnedNullOrForbidden(GatherTargetIndex)
                .FailOnBurningImmobile(GatherTargetIndex)
                .FailOn(() => IsBillDisabled);

            // 자원 채집
            var gatherWorkAmount = Mathf.CeilToInt(GatheringRecipeDef.GatheringWorkAmount / (job.RecipeDef.efficiencyStat != null ? pawn.GetStatValue(job.RecipeDef.efficiencyStat) : 1f));
            yield return Toils_General.Wait(gatherWorkAmount, GatherTargetIndex)
                .FailOnDespawnedNullOrForbidden(GatherTargetIndex)
                .FailOnBurningImmobile(GatherTargetIndex)
                .FailOn(() => IsBillDisabled)
                .WithInitAction(() =>
                {
                    job.bill.Notify_BillWorkStarted(pawn);
                })
                .WithFailCondition(() => !GatherTargetInfo.Thing.CanGatherable(GatheringRecipeDef.targetYieldStat, GatheringRecipeDef.targetCooldownStat))
                .WithEffect(() => GetActor().CurJob.bill.recipe.effectWorking, TargetIndex.A)
                .WithProgressBarToilDelay(GatherTargetIndex);

            // 채집 체크
            yield return new Toil()
                .WithDefaultCompleteMode(ToilCompleteMode.Instant)
                .WithInitAction(() =>
                {
                    GatheringRecipeDef.gatherWorker.Notify_Gathered(pawn, BillGiver, GatherTargetInfo.Thing);

                    if (GatheringRecipeDef.targetYieldStat != null)
                    {
                        gatherEfficiencyList.Add(GatherTargetInfo.Thing.GetStatValue(GatheringRecipeDef.targetYieldStat));
                    }
                    else
                    {
                        gatherEfficiencyList.Add(1f);
                    }
                });

            if (GatheringRecipeDef.ProcessingWorkAmount > 0)
            {
                // 가공이 필요한 경우 작업대로 이동
                yield return Toils_Goto.GotoThing(BillGiverTargetIndex, PathEndMode.InteractionCell)
                    .FailOnDespawnedNullOrForbidden(BillGiverTargetIndex)
                    .FailOnBurningImmobile(BillGiverTargetIndex)
                    .FailOn(() => IsBillDisabled);

                // 작업대에서 결과물을 가공
                var processWorkAmount = Mathf.CeilToInt(GatheringRecipeDef.ProcessingWorkAmount / pawn.GetStatValue(StatDefOf.WorkSpeedGlobal));
                yield return Toils_General.Wait(processWorkAmount, BillGiverTargetIndex)
                    .FailOnDespawnedNullOrForbidden(BillGiverTargetIndex)
                    .FailOnBurningImmobile(BillGiverTargetIndex)
                    .FailOn(() => IsBillDisabled)
                    .WithInitAction(() =>
                    {
                        pawn.rotationTracker.FaceTarget(BillGiver);
                    })
                    .WithTickAction(() =>
                    {
                        job.bill.Notify_PawnDidWork(pawn);
                        if (BillGiver is IBillGiverWithTickAction billGiverWithTickAction)
                        {
                            billGiverWithTickAction.UsedThisTick();
                        }
                    })
                    .WithHandlingFacing()
                    .WithProgressBarToilDelay(BillGiverTargetIndex, interpolateBetweenActorAndTarget: true)
                    .WithActiveSkill(() => job.bill.recipe.workSkill)
                    .FailOnCannotTouch(BillGiverTargetIndex, PathEndMode.InteractionCell)
                    .PlaySustainerOrSound(() => GetActor().CurJob.bill.recipe.soundWorking);

                // 작업대에서 결과물을 저장
                yield return new Toil()
                    .WithDefaultCompleteMode(ToilCompleteMode.Instant)
                    .WithInitAction(() =>
                    {
                        var actor = GetActor();
                        var curJob = actor.jobs.curJob;
                        var billGiver = BillGiver;

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
                            if (BillGiver is Building_WorkTable building_WorkTable)
                            {
                                efficiency *= building_WorkTable.GetStatValue(curJob.RecipeDef.workTableEfficiencyStat);
                            }
                        }

                        if (GatheringRecipeDef.targetYieldStat != null)
                        {
                            efficiency *= gatherEfficiencyList.Sum();
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
            else
            {
                yield return new Toil()
                    .WithDefaultCompleteMode(ToilCompleteMode.Instant)
                    .WithInitAction(() =>
                    {
                        var actor = GetActor();
                        var curJob = actor.jobs.curJob;
                        var billGiver = BillGiver;

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
                            if (BillGiver is Building_WorkTable building_WorkTable)
                            {
                                efficiency *= building_WorkTable.GetStatValue(curJob.RecipeDef.workTableEfficiencyStat);
                            }
                        }

                        if (GatheringRecipeDef.targetYieldStat != null)
                        {
                            efficiency *= GatherTargetInfo.Thing.GetStatValue(GatheringRecipeDef.targetYieldStat);
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
}
