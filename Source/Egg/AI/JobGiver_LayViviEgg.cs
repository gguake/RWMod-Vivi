using RimWorld;
using Verse;
using Verse.AI;

namespace VVRace
{
    public class JobGiver_LayViviEgg : ThinkNode_JobGiver
    {
        public override float GetPriority(Pawn pawn)
        {
            var compViviEggLayer = pawn.GetCompViviEggLayer();
            return compViviEggLayer != null && compViviEggLayer.CanLayEgg ? 10f : 0f;
        }

        protected override Job TryGiveJob(Pawn pawn)
        {
            var compViviEggLayer = pawn.GetCompViviEggLayer();
            if (compViviEggLayer == null || !compViviEggLayer.CanLayEgg)
            {
                return null;
            }

            if ((pawn.Faction == Faction.OfPlayerSilentFail || pawn.GetNestLordJob() != null) && TryGetBestEggBox(pawn, out var eggBox))
            {
                return JobMaker.MakeJob(VVJobDefOf.VV_LayViviEgg, eggBox);
            }

            return null;
        }

        private bool TryGetBestEggBox(Pawn pawn, out Thing eggBox)
        {
            // 영역 탐색에서 찾지 못하면 맵 전체의 부화기를 도달 가능한지 검사한다 (영역 수 제한 없음).
            eggBox = GenClosest.ClosestThingReachable(
                pawn.Position,
                pawn.Map,
                ThingRequest.ForDef(VVThingDefOf.VV_ViviHatchery),
                PathEndMode.OnCell,
                TraverseParms.For(pawn, Danger.Some),
                validator: (thing) =>
                {
                    // 다른 세력의 부화기에서는 알이 부화하지 못하고 사라진다.
                    if (!thing.Spawned || thing.Faction != pawn.Faction || thing.IsForbidden(pawn) || !pawn.CanReserve(thing))
                    {
                        return false;
                    }

                    return thing is ViviEggHatchery hatchery && hatchery.CanLayHere;
                },
                searchRegionsMin: 10);

            return eggBox != null;
        }
    }
}
