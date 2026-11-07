using RimWorld;
using System.Collections.Generic;
using Verse;
using Verse.AI;

namespace VVRace
{
    // 둥지 활동 범위(ViviNestTerritory가 칠한 칸) 안의 불을 끈다. 바닐라 JobGiver_FightFiresNearPoint(듀티 지점 한 점에서의 반경)를 칠한 칸으로 바꾼 것으로,
    // 고르는 기준(둥지 중심에서 가장 가깝고 닿을 수 있는 불, 폰에 붙은 불 제외, 예약·불 끄기 가능)은 바닐라와 같다.
    public class JobGiver_ViviNestFightFires : ThinkNode_JobGiver
    {
        private static readonly List<Thing> _tmpFires = new List<Thing>();

        protected override Job TryGiveJob(Pawn pawn)
        {
            var nest = pawn.GetNestLordJob();
            if (nest == null || !nest.Settled || pawn.WorkTagIsDisabled(WorkTags.Firefighting)) { return null; }

            var map = pawn.Map;
            var fires = map.listerThings.ThingsOfDef(ThingDefOf.Fire);
            if (fires.Count == 0) { return null; }

            var territory = nest.Territory;
            _tmpFires.Clear();
            for (int i = 0; i < fires.Count; ++i)
            {
                var fire = fires[i];
                if (((AttachableThing)fire).parent is Pawn || !territory.InWorkRange(fire.Position)) { continue; }
                _tmpFires.Add(fire);
            }
            if (_tmpFires.Count == 0) { return null; }

            var target = GenClosest.ClosestThing_Global_Reachable(nest.NestCell, map, _tmpFires, PathEndMode.Touch, TraverseParms.For(pawn),
                validator: t => pawn.CanReserve(t));
            _tmpFires.Clear();
            return target != null ? JobMaker.MakeJob(JobDefOf.BeatFire, target) : null;
        }
    }
}
