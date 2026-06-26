using System.Collections.Generic;
using Verse;
using Verse.AI;

namespace VVRace
{
    public class JobDriver_LayViviEgg : JobDriver
    {
        private const int LayingEggTicks = 500;
        private const TargetIndex HatcheryIdx = TargetIndex.A;
        private LocalTargetInfo Hatchery => job.GetTarget(HatcheryIdx);

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            return pawn.Reserve(Hatchery, job, errorOnFailed: errorOnFailed);
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOnDespawnedNullOrForbidden(HatcheryIdx);
            this.FailOn(() =>
            {
                var hatchery = TargetA.Thing as ViviEggHatchery;
                var eggSpawner = pawn.GetCompViviEggLayer();
                return eggSpawner == null || !eggSpawner.CanLayEgg || !hatchery.CanLayHere;
            });

            yield return Toils_Goto.GotoCell(HatcheryIdx, PathEndMode.OnCell);
            yield return Toils_General.Wait(LayingEggTicks);
            yield return Toils_General.Do(delegate
            {
                var eggSpawner = pawn.GetCompViviEggLayer();
                if (eggSpawner != null && eggSpawner.CanLayEgg)
                {
                    var egg = eggSpawner.ProduceEgg();
                    if (egg != null)
                    {
                        var hatchery = Hatchery.Thing as ViviEggHatchery;
                        if (hatchery != null && hatchery.CanLayHere)
                        {
                            hatchery.ViviEgg = egg;
                        }
                        else
                        {
                            GenPlace.TryPlaceThing(egg, Hatchery.Cell, pawn.Map, ThingPlaceMode.Near);
                        }

                        return;
                    }
                }

                Log.Warning($"{pawn} tried to lay egg but there is no egg created. something wrong");
            });
        }
    }
}
