using RimWorld;
using System.Collections.Generic;
using Verse;
using Verse.AI;

namespace VVRace
{
    public class JobDriver_PlantArcaneSeed : JobDriver
    {
        public const float TotalWork = 500f;

        private const TargetIndex SeedIndex = TargetIndex.A;
        private const TargetIndex SeedlingCellIndex = TargetIndex.B;
        private const TargetIndex OldSeedIndex = TargetIndex.C;

        private Thing Seed => job.GetTarget(SeedIndex).Thing;

        private IntVec3 SeedlingCell => job.GetTarget(SeedlingCellIndex).Cell;

        private float _workDone;

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref _workDone, "workDone");
        }

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            return pawn.Reserve(Seed, job, 1, 1, null, errorOnFailed);
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOn(() => 
                !Seed.TryGetComp<CompArcaneSeed>().CanSowAt(SeedlingCell, Map) || 
                !Seed.TryGetComp<CompArcaneSeed>().SeedlingCells.Contains(SeedlingCell));

            yield return Toils_Goto.GotoThing(SeedIndex, PathEndMode.Touch);
            yield return Toils_General.Do(() =>
            {
                var seed = job.GetTarget(SeedIndex);
                if (seed.HasThing && seed.Thing.stackCount > job.count)
                {
                    job.SetTarget(OldSeedIndex, seed.Thing);
                }
            });

            yield return Toils_Haul.StartCarryThing(SeedIndex);
            yield return Toils_General.Do(() =>
            {
                var cell = SeedlingCell;
                var oldSeed = job.GetTarget(OldSeedIndex);
                if (oldSeed.IsValid && oldSeed.HasThing)
                {
                    oldSeed.Thing.TryGetComp<CompArcaneSeed>()?.SeedlingCells.Remove(cell);
                }

                var compArcaneSeed = Seed.TryGetComp<CompArcaneSeed>();
                compArcaneSeed.SeedlingCells.Clear();
                compArcaneSeed.SeedlingCells.Add(cell);
            });

            yield return Toils_Haul.CarryHauledThingToCell(SeedlingCellIndex, PathEndMode.Touch);

            yield return ToilMaker.MakeToil("JobDriver_PlantArcaneSeed_Work")
                .WithTickIntervalAction((delta) =>
                {
                    _workDone += pawn.GetStatValue(StatDefOf.PlantWorkSpeed) * delta;

                    if (pawn.skills != null)
                    {
                        pawn.skills.Learn(SkillDefOf.Plants, 0.05f * delta);
                    }
                    if (_workDone >= TotalWork)
                    {
                        ReadyForNextToil();
                    }
                })
                .WithProgressBar(SeedlingCellIndex, () => _workDone / TotalWork, interpolateBetweenActorAndTarget: true)
                .WithEffect(EffecterDefOf.Sow, SeedlingCellIndex)
                .PlaySustainerOrSound(SoundDefOf.Interact_Sow)
                .WithActiveSkill(() => SkillDefOf.Plants)
                .WithDefaultCompleteMode(ToilCompleteMode.Never);

            yield return Toils_General.Do(() =>
            {
                var compArcaneSeed = Seed.TryGetComp<CompArcaneSeed>();

                var seedling = (ArcanePlant_Seedling)ThingMaker.MakeThing(VVThingDefOf.VV_ArcanePlantSeedling);
                seedling.SetMaturePlant(compArcaneSeed.Props.targetPlantDef);

                if (GenPlace.TryPlaceThing(seedling, SeedlingCell, Map, ThingPlaceMode.Direct))
                {
                    Map.mapDrawer.MapMeshDirty(SeedlingCell, MapMeshFlagDefOf.Things);
                    Seed.Destroy();
                }
            });
        }
    }
}
