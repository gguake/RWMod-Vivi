using RimWorld;
using System.Collections.Generic;
using Verse;
using Verse.AI;

namespace VVRace
{
    public class JobDriver_ViviNestDigFertilizer : JobDriver
    {
        private const float WorkAmount = 2000f;
        private const int FertilizerYield = 10;

        private float _workDone;

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref _workDone, "workDone");
        }

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            return pawn.Reserve(job.targetA, job, errorOnFailed: errorOnFailed);
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            yield return Toils_Goto.GotoCell(TargetIndex.A, PathEndMode.OnCell);

            var dig = ToilMaker.MakeToil("VV_DigFertilizer");
            dig.tickIntervalAction = delta =>
            {
                _workDone += pawn.GetStatValue(StatDefOf.GeneralLaborSpeed) * delta;
                pawn.skills?.Learn(SkillDefOf.Plants, 0.02f * delta);
                if (_workDone >= WorkAmount)
                {
                    ReadyForNextToil();
                }
            };
            dig.defaultCompleteMode = ToilCompleteMode.Never;
            dig.WithEffect(EffecterDefOf.ConstructDirt, TargetIndex.A);
            dig.WithProgressBar(TargetIndex.A, () => _workDone / WorkAmount);
            dig.activeSkill = () => SkillDefOf.Plants;
            yield return dig;

            yield return Toils_General.Do(() =>
            {
                var fertilizer = ThingMaker.MakeThing(VVThingDefOf.VV_Fertilizer);
                fertilizer.stackCount = FertilizerYield;
                BeekeepingUtility.PlaceProduct(pawn, fertilizer, job.targetA.Cell);
                FilthMaker.TryMakeFilth(job.targetA.Cell, Map, ThingDefOf.Filth_Dirt);
            });
        }
    }
}
