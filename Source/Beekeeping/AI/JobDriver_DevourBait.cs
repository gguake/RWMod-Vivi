using RimWorld;
using System.Collections.Generic;
using System.Linq;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace VVRace
{
    // 바닐라 섭취는 식성(곰은 동물성 산물·액체를 못 먹음)에 막히므로, 미끼 도둑은 유인통 위 미끼를 통째로 먹어 치운다.
    public class JobDriver_DevourBait : JobDriver
    {
        private const int DevourTicks = 300;

        private Building_ViviLure Lure => job.GetTarget(TargetIndex.A).Thing as Building_ViviLure;

        public override bool TryMakePreToilReservations(bool errorOnFailed) => true;

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOnDespawnedOrNull(TargetIndex.A);
            this.FailOn(() => !Lure.HasBait);

            yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.Touch);

            yield return Toils_General.Wait(DevourTicks, TargetIndex.A)
                .WithEffect(() => EffecterDefOf.EatMeat, TargetIndex.A);

            yield return Toils_General.Do(() =>
            {
                var lure = Lure;
                var nutrition = 0f;
                foreach (var bait in lure.BaitThings.ToList())
                {
                    nutrition += bait.GetStatValue(StatDefOf.Nutrition) * bait.stackCount;
                    bait.Destroy();
                }

                if (pawn.needs?.food != null)
                {
                    pawn.needs.food.CurLevel += nutrition;
                }

                pawn.GetLord()?.ReceiveMemo(LordJob_ViviBaitThief.MemoBaitDevoured);
            });
        }
    }
}
