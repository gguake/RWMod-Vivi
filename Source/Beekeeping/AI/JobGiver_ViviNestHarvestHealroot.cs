using RimWorld;
using System.Collections.Generic;
using Verse;
using Verse.AI;

namespace VVRace
{
    // 약이 모자라면 둥지 근처의 다 자란 약뿌리(야생·재배)를 바닐라 수확 잡으로 거둔다(거둔 약초는 수확 알림에서 둥지 몫이 된다).
    // 플레이어 재배 구역의 약뿌리는 건드리지 않는다. 우선도는 약이 목표에 비해 모자란 정도다(보유량 균형).
    // 반경 안에 거둘 약뿌리가 없으면 자라는 약뿌리가 거둘 만큼 자랄 때(상한 HealrootRetryMaxTicks)까지 우선도를 0으로 둔다(ViviNestProduction).
    public class JobGiver_ViviNestHarvestHealroot : ThinkNode_JobGiver
    {
        private const float SearchRadius = 24f;

        private static List<ThingDef> _healrootDefs;

        // 약초를 주는 식물 정의(야생·재배 약뿌리, 다른 모드의 변종). 반경 안의 모든 식물(풀 포함)을 지역 BFS로 훑던 것을 이 정의의 식물만 본다
        // (실측: 약이 0인 둥지에서 늘 실패하며 1회 17~30 µs, 반경 안 지역이 30개를 넘으면 맵 전체 식물 전역 탐색으로 넘어가 1회 390~430 µs).
        private static List<ThingDef> HealrootDefs
        {
            get
            {
                if (_healrootDefs == null)
                {
                    _healrootDefs = new List<ThingDef>();
                    foreach (var def in DefDatabase<ThingDef>.AllDefsListForReading)
                    {
                        if (def.category == ThingCategory.Plant && def.plant != null && def.plant.harvestedThingDef == ThingDefOf.MedicineHerbal)
                        {
                            _healrootDefs.Add(def);
                        }
                    }
                }
                return _healrootDefs;
            }
        }

        // 약뿌리를 거둘 수 있는 둥지(GetPriority·TryGiveJob 공통 조건): 정착했고, 아기가 아니며 재배를 할 수 있고, 바깥 일을 쉬지 않을 때
        private static LordJob_ViviNest WorkingNest(Pawn pawn)
        {
            var nest = pawn.GetNestLordJob();
            if (nest == null || !nest.Settled || pawn.DevelopmentalStage.Baby() || pawn.WorkTypeIsDisabled(WorkTypeDefOf.Growing)) { return null; }
            return BeekeepingUtility.OutdoorWorkPaused(pawn) ? null : nest;
        }

        public override float GetPriority(Pawn pawn)
        {
            var nest = WorkingNest(pawn);
            if (nest == null || nest.Production.HealrootWaiting) { return 0f; }

            return nest.Production.MedicineNeed;
        }

        protected override Job TryGiveJob(Pawn pawn)
        {
            var nest = WorkingNest(pawn);
            if (nest == null) { return null; }
            var production = nest.Production;
            if (production.MedicineNeed <= 0f || production.HealrootWaiting) { return null; }

            var plant = FindHealroot(pawn, nest, out var anyTarget, out var readyTick);
            if (plant != null) { return JobMaker.MakeJob(JobDefOf.Harvest, plant); }

            // 거둘 약뿌리가 아예 없을 때만 둥지 전체가 기다린다(있는데 이 비비가 예약·도달하지 못한 것이면 다른 구성원은 거둘 수 있다).
            if (!anyTarget) { production.Notify_NoHealroot(readyTick); }
            return null;
        }

        // 활동 범위 중심(둥지 중심·증축 방 중심) 어디서든 반경 안, 구역 밖의 다 자란 약뿌리 중 이 비비가 쓸 수 있는(금지되지 않고 예약·도달할 수 있는) 가장 가까운 것.
        // anyTarget은 비비와 상관없이 거둘 수 있는 약뿌리가 하나라도 있었는지, readyTick은 아직 덜 자란 약뿌리가 가장 먼저 거둘 만큼 자랄 틱(모르면 -1)이다.
        private static Plant FindHealroot(Pawn pawn, LordJob_ViviNest nest, out bool anyTarget, out int readyTick)
        {
            anyTarget = false;
            readyTick = -1;

            var map = pawn.Map;
            var root = nest.NestCell;
            var territory = nest.Territory;
            var traverse = TraverseParms.For(pawn, Danger.Some);
            const int maxDistance = (int)(SearchRadius * SearchRadius);
            Plant best = null;
            var bestDistance = maxDistance;
            var defs = HealrootDefs;
            for (int d = 0; d < defs.Count; ++d)
            {
                var things = map.listerThings.ThingsOfDef(defs[d]);
                for (int i = 0; i < things.Count; ++i)
                {
                    if (!(things[i] is Plant plant)) { continue; }

                    // 활동 범위 중심(둥지 중심·증축 방 중심) 중 가장 가까운 것까지의 거리
                    var distance = territory.ActivityDistSq(plant.Position);
                    if (distance >= maxDistance || map.zoneManager.ZoneAt(plant.Position) != null) { continue; }
                    if (!plant.HarvestableNow)
                    {
                        readyTick = BeekeepingUtility.EarlierTick(readyTick, BeekeepingUtility.HarvestReadyTick(plant, ViviNestProduction.HealrootRetryMaxTicks));
                        continue;
                    }

                    anyTarget = true;
                    if (distance >= bestDistance) { continue; }
                    if (plant.IsForbidden(pawn) || !pawn.CanReserve(plant) || !map.reachability.CanReach(root, plant, PathEndMode.Touch, traverse)) { continue; }

                    best = plant;
                    bestDistance = distance;
                }
            }
            return best;
        }
    }
}
