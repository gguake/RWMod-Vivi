using HarmonyLib;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Reflection;
using Verse;
using Verse.AI;

namespace VVRace
{
    // NPC는 바닐라 옷 고르기를 하지 않으므로 둥지 비비의 옷·무기를 바닐라 규칙으로 고른다(사용자 요구 "근처에 옷이나 장비가 있으면 입도록, 옷 입는 규칙은 바닐라와 동일").
    // 고르는 것은 둥지 비비가 쓸 수 있는 옷·무기(금지 판정, LordJob_ViviNest.CanUse): 둥지 몫과 둥지 둘레에 놓인 것(IsFreeGear, 플레이어가 두어 입게 할 수 있다)이다.
    // - 옷: 정착민 옷 고르기(JobGiver_OptimizeApparel)와 같은 후보 조건·점수로 가장 나은 것을 입고, 바닐라처럼 6000~9000틱마다 다시 본다.
    //   둥지가 만든 기본옷·아동복은 그 옷 자리가 빈 비비(NeedsCostume)가 기다리지 않고 바로 본다.
    // - 무기: NPC 무기 줍기(JobGiver_PickUpOpportunisticWeapon)의 판정으로 지금 무기보다 나은 것(원거리 > 근접 > 맨손)을 가장 가까운 것부터 든다.
    //   둥지가 만든 새총은 새총잡이(NeedsSlingshot) 몫이라 바로 보고, 다른 구성원은 둥지 둘레에 놓인 무기만 본다(둥지 새총을 일반 비비가 가져가 새총잡이가 기다리지 않게).
    public class JobGiver_ViviNestDress : ThinkNode_JobGiver
    {
        private const int OptimizeIntervalMin = 6000;
        private const int OptimizeIntervalMax = 9000;
        private const float MinScoreGainToCare = 0.05f;

        // 바닐라 옷 점수(ApparelScoreRaw)가 보는 보온 필요. 정착민 옷 고르기는 점수를 매기기 전에 그 폰 기준으로 정해 둔다.
        private static readonly FieldInfo FieldInfo_neededWarmth = AccessTools.Field(typeof(JobGiver_OptimizeApparel), "neededWarmth");

        private static readonly JobGiver_PickUpOpportunisticWeapon _weaponGiver = new JobGiver_PickUpOpportunisticWeapon();
        private static readonly Func<Pawn, bool> AlreadySatisfiedWithCurrentWeapon =
            AccessTools.MethodDelegate<Func<Pawn, bool>>(AccessTools.Method(typeof(JobGiver_PickUpOpportunisticWeapon), "AlreadySatisfiedWithCurrentWeapon"), _weaponGiver);
        private static readonly Func<Thing, Pawn, bool> ShouldEquipWeapon =
            AccessTools.MethodDelegate<Func<Thing, Pawn, bool>>(AccessTools.Method(typeof(JobGiver_PickUpOpportunisticWeapon), "ShouldEquipWeapon"), _weaponGiver);

        private static readonly List<float> _wornScores = new List<float>();

        protected override Job TryGiveJob(Pawn pawn)
        {
            var nest = pawn.GetNestLordJob();
            if (nest == null || !nest.Settled || pawn.apparel == null || pawn.DevelopmentalStage.Baby()) { return null; }

            var map = pawn.Map;
            var job = (ViviNestProduction.NeedsCostume(pawn) ? WearJob(pawn, map.listerThings.ThingsOfDef(ViviNestProduction.CostumeFor(pawn))) : null)
                ?? (ViviNestProduction.NeedsSlingshot(pawn) ? EquipJob(pawn, nest, map.listerThings.ThingsOfDef(VVThingDefOf.VV_Slingshot), nestOwned: true) : null);
            if (job != null) { return job; }

            var ticks = Find.TickManager.TicksGame;
            if (ticks < pawn.mindState.nextApparelOptimizeTick) { return null; }
            pawn.mindState.nextApparelOptimizeTick = ticks + Rand.Range(OptimizeIntervalMin, OptimizeIntervalMax);

            return WearJob(pawn, map.listerThings.ThingsInGroup(ThingRequestGroup.Apparel))
                ?? EquipJob(pawn, nest, map.listerThings.ThingsInGroup(ThingRequestGroup.Weapon), nestOwned: false);
        }

        // 바닐라 정착민 옷 고르기(JobGiver_OptimizeApparel.TryGiveJob)의 후보 조건과 점수. NPC에게는 옷 정책이 없어(모든 옷 허용) 정책 검사는 빼고,
        // 보관 칸에 있어야 한다는 조건 대신 둥지 비비가 쓸 수 있는 옷만 본다.
        private static Job WearJob(Pawn pawn, List<Thing> candidates)
        {
            if (candidates.Count == 0) { return null; }

            FieldInfo_neededWarmth.SetValue(null, PawnApparelGenerator.CalculateNeededWarmth(pawn, pawn.Map.Tile, GenLocalDate.Twelfth(pawn)));
            var worn = pawn.apparel.WornApparel;
            _wornScores.Clear();
            for (int i = 0; i < worn.Count; ++i)
            {
                _wornScores.Add(JobGiver_OptimizeApparel.ApparelScoreRaw(pawn, worn[i]));
            }

            Thing best = null;
            var bestGain = 0f;
            for (int i = 0; i < candidates.Count; ++i)
            {
                var apparel = (Apparel)candidates[i];
                if (apparel.IsForbidden(pawn) || apparel.IsBurning() || (apparel.def.apparel.gender != Gender.None && apparel.def.apparel.gender != pawn.gender)) { continue; }

                var gain = ApparelScoreGain(pawn, apparel);
                if (gain < MinScoreGainToCare || gain < bestGain || (CompBiocodable.IsBiocoded(apparel) && !CompBiocodable.IsBiocodedFor(apparel, pawn))) { continue; }
                if (!ApparelUtility.HasPartsToWear(pawn, apparel.def) || !apparel.def.apparel.developmentalStageFilter.Has(pawn.DevelopmentalStage)) { continue; }
                if (!pawn.CanReserveAndReach(apparel, PathEndMode.OnCell, Danger.Some)) { continue; }

                best = apparel;
                bestGain = gain;
            }
            _wornScores.Clear();

            return best != null ? JobMaker.MakeJob(JobDefOf.Wear, best) : null;
        }

        // 바닐라 JobGiver_OptimizeApparel.ApparelScoreGain과 같은 계산. 바닐라 것은 함께 못 입는 옷을 벗을 수 있는지를 강제 착용 추적기(outfits)로 보는데
        // NPC에게는 그 추적기가 없어 그대로 부를 수 없다(강제로 입힌 옷이 없으므로 잠긴 옷만 본다).
        private static float ApparelScoreGain(Pawn pawn, Apparel apparel)
        {
            if (apparel.def == ThingDefOf.Apparel_ShieldBelt && pawn.equipment?.Primary != null && pawn.equipment.Primary.def.IsWeaponUsingProjectiles) { return -1000f; }
            if (apparel.def.apparel.ignoredByNonViolent && pawn.WorkTagIsDisabled(WorkTags.Violent)) { return -1000f; }

            var score = JobGiver_OptimizeApparel.ApparelScoreRaw(pawn, apparel);
            var worn = pawn.apparel.WornApparel;
            var replacing = false;
            for (int i = 0; i < worn.Count; ++i)
            {
                if (ApparelUtility.CanWearTogether(worn[i].def, apparel.def, pawn.RaceProps.body)) { continue; }
                if (pawn.apparel.IsLocked(worn[i])) { return -1000f; }

                score -= _wornScores[i];
                replacing = true;
            }
            return replacing ? score : score * 10f;
        }

        // 바닐라 NPC 무기 줍기(JobGiver_PickUpOpportunisticWeapon.TryGiveJob)와 같은 판정. 바닐라는 폰 둘레 8칸을 보지만 둥지 비비는 쓸 수 있는 무기 중 가장 가까운 것을 든다.
        // nestOwned: 둥지가 만든 무기(새총잡이 몫)를 볼지, 둥지 둘레에 놓인 무기를 볼지
        private static Job EquipJob(Pawn pawn, LordJob_ViviNest nest, List<Thing> candidates, bool nestOwned)
        {
            if (pawn.equipment == null || candidates.Count == 0) { return null; }
            if (pawn.WorkTagIsDisabled(WorkTags.Violent) || !pawn.health.capacities.CapableOf(PawnCapacityDefOf.Manipulation) || AlreadySatisfiedWithCurrentWeapon(pawn)) { return null; }

            Thing best = null;
            var bestDistance = int.MaxValue;
            for (int i = 0; i < candidates.Count; ++i)
            {
                var weapon = candidates[i];
                if (nest.Stock.IsNestOwned(weapon) != nestOwned || weapon.IsForbidden(pawn) || weapon.IsBurning() || !ShouldEquipWeapon(weapon, pawn)) { continue; }

                var distance = weapon.Position.DistanceToSquared(pawn.Position);
                if (distance >= bestDistance || !pawn.CanReserveAndReach(weapon, PathEndMode.OnCell, Danger.Some)) { continue; }

                best = weapon;
                bestDistance = distance;
            }

            return best != null ? JobMaker.MakeJob(JobDefOf.Equip, best) : null;
        }
    }
}
