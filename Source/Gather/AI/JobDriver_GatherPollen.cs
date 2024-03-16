using RimWorld;
using System.Collections.Generic;
using UnityEngine;
using Verse;
using Verse.AI;

namespace VVRace
{
    public class JobDriver_GatherPollen : JobDriver
    {
        protected const TargetIndex FilthTargetIndex = TargetIndex.A;
        protected const TargetIndex GathererBuildingTargetIndex = TargetIndex.B;
        protected const TargetIndex StorageCellTargetIndex = TargetIndex.C;

        protected LocalTargetInfo FilthTargetInfo => job.GetTarget(FilthTargetIndex);
        protected LocalTargetInfo GathererBuildingTargetInfo => job.GetTarget(GathererBuildingTargetIndex);

        protected Thing Filth => FilthTargetInfo.Thing;
        protected Thing GathererBuilding => GathererBuildingTargetInfo.Thing;

        private int TotalWorkAmount => (int)job.bill.recipe.workAmount;

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

                if (!(GathererBuilding is IBillGiver billGiver)) { return true; }
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
            // 대상으로 이동
            yield return Toils_Goto.GotoThing(FilthTargetIndex, PathEndMode.Touch)
                .FailOnDespawnedNullOrForbidden(FilthTargetIndex)
                .FailOn(() => IsBillDisabled);

            // 대상을 청소
            var gatherWorkAmount = Mathf.CeilToInt(TotalWorkAmount / (job.RecipeDef.efficiencyStat != null ? pawn.GetStatValue(job.RecipeDef.efficiencyStat) : 1f));
            yield return Toils_General.Wait(gatherWorkAmount, FilthTargetIndex)
                .FailOnDespawnedNullOrForbidden(FilthTargetIndex)
                .FailOn(() => IsBillDisabled)
                .WithInitAction(() =>
                {
                    job.bill.Notify_BillWorkStarted(pawn);
                })
                .WithEffect(() => GetActor().CurJob.bill.recipe.effectWorking, TargetIndex.A)
                .WithProgressBarToilDelay(FilthTargetIndex);

            // 꽃가루 생성 및 저장
            yield return new Toil()
                .WithInitAction(() =>
                {
                    var filth = Filth as Filth;
                    if (filth != null)
                    {
                        filth.ThinFilth();
                    }

                    var actor = GetActor();
                    var curJob = actor.jobs.curJob;
                    var billGiver = GathererBuilding;

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
                        if (GathererBuilding is Building_WorkTable building_WorkTable)
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
                                StoreUtility.TryFindBestBetterStoreCellForIn(allProducts[0], actor, actor.Map, StoragePriority.Unstored, actor.Faction, curJob.bill.GetSlotGroup(), out foundCell);
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
                })
                .WithDefaultCompleteMode(ToilCompleteMode.Instant);

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
