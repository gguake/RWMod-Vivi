using RimWorld;
using System.Collections.Generic;
using UnityEngine;
using Verse;
using Verse.AI;

namespace VVRace
{
    public class JobDriver_FortifyHoneycombWall : JobDriver
    {
        public const int BaseWorkAmount = 5000;

        private float workLeft = -1000f;

        private LocalTargetInfo WallTarget => job.GetTarget(WallIndex).Thing;
        private List<LocalTargetInfo> IngredientTargetQueue => job.GetTargetQueue(IngredientIndex);

        private const TargetIndex WallIndex = TargetIndex.A;
        private const TargetIndex IngredientIndex = TargetIndex.B;

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            pawn.ReserveAsManyAsPossible(IngredientTargetQueue, job);

            return pawn.Reserve(job.targetA, job, errorOnFailed: errorOnFailed);
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOn(() => !job.ignoreDesignations && Map.designationManager.DesignationOn(WallTarget.Thing, VVDesignationDefOf.VV_FortifyHoneycombWall) == null);
            this.FailOnDespawnedNullOrForbidden(WallIndex);

            yield return Toils_General.DoAtomic(delegate
            {
                job.count = WorkGiver_FortifyHoneycombWall.IngredientLifestrandAmount;
            });
            var getNextIngredient = Toils_General.Label();
            yield return getNextIngredient;
            yield return Toils_JobTransforms.ExtractNextTargetFromQueue(IngredientIndex);
            yield return Toils_Goto.GotoThing(IngredientIndex, PathEndMode.ClosestTouch)
                .FailOnDespawnedNullOrForbidden(IngredientIndex)
                .FailOnSomeonePhysicallyInteracting(IngredientIndex);

            yield return Toils_Haul.StartCarryThing(IngredientIndex, putRemainderInQueue: false, subtractNumTakenFromJobCount: true)
                .FailOnDestroyedNullOrForbidden(IngredientIndex);

            yield return Toils_Jump.JumpIf(getNextIngredient, () => !job.GetTargetQueue(TargetIndex.B).NullOrEmpty());

            yield return Toils_Goto.GotoThing(WallIndex, PathEndMode.Touch);
            yield return ToilMaker.MakeToil(nameof(JobDriver_FortifyHoneycombWall))
                .WithInitAction(() =>
                {
                    workLeft = BaseWorkAmount;
                })
                .WithTickAction(() =>
                {
                    var workByTick = pawn.GetStatValue(StatDefOf.ConstructionSpeed) * 1.7f;
                    workLeft -= workByTick;

                    if (pawn.skills != null)
                    {
                        pawn.skills.Learn(SkillDefOf.Construction, 0.1f);
                    }

                    if (workLeft <= 0f)
                    {
                        var wall = WallTarget.Thing;
                        var currentHealthPct = (float)wall.HitPoints / wall.MaxHitPoints;

                        var map = wall.Map;
                        var position = wall.Position;
                        var rotation = wall.Rotation;
                        var faction = wall.Faction;

                        wall.Destroy(DestroyMode.WillReplace);
                        var newWall = ThingMaker.MakeThing(VVThingDefOf.VV_ViviHardenHoneycombWall);
                        newWall.SetFaction(faction ?? Faction.OfPlayerSilentFail);
                        newWall.HitPoints = Mathf.Clamp(Mathf.CeilToInt(currentHealthPct * newWall.MaxHitPoints), 1, newWall.MaxHitPoints);

                        GenSpawn.Spawn(newWall, position, map, rotation);

                        pawn.carryTracker.CarriedThing.Destroy();
                        ReadyForNextToil();
                    }
                })
                .FailOnCannotTouch(WallIndex, PathEndMode.Touch)
                .WithProgressBar(WallIndex, () => 1f - workLeft / (float)BaseWorkAmount)
                .WithDefaultCompleteMode(ToilCompleteMode.Never)
                .WithActiveSkill(() => SkillDefOf.Construction);
        }
    }
}
