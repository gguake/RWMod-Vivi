using HarmonyLib;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Reflection;
using Verse;
using Verse.AI;

namespace VVRace
{
    public class JobGiver_ViviNestDress : ThinkNode_JobGiver
    {
        private const int OptimizeIntervalMin = 6000;
        private const int OptimizeIntervalMax = 9000;
        private const float MinScoreGainToCare = 0.05f;

        private static readonly FieldInfo JobGiverOptimizeApparelNeededWarmthField = AccessTools.Field(typeof(JobGiver_OptimizeApparel), "neededWarmth");

        private static readonly JobGiver_PickUpOpportunisticWeapon _weaponGiver = new JobGiver_PickUpOpportunisticWeapon();
        
        private static readonly Func<Pawn, bool> AlreadySatisfiedWithCurrentWeapon =
            AccessTools.MethodDelegate<Func<Pawn, bool>>(
                AccessTools.Method(typeof(JobGiver_PickUpOpportunisticWeapon), "AlreadySatisfiedWithCurrentWeapon"), 
                _weaponGiver);
        
        private static readonly Func<Thing, Pawn, bool> ShouldEquipWeapon =
            AccessTools.MethodDelegate<Func<Thing, Pawn, bool>>(
                AccessTools.Method(typeof(JobGiver_PickUpOpportunisticWeapon), "ShouldEquipWeapon"), 
                _weaponGiver);

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

        private static Job WearJob(Pawn pawn, List<Thing> candidates)
        {
            if (candidates.Count == 0) { return null; }

            JobGiverOptimizeApparelNeededWarmthField.SetValue(null, PawnApparelGenerator.CalculateNeededWarmth(pawn, pawn.Map.Tile, GenLocalDate.Twelfth(pawn)));
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
                if (!EquipmentUtility.CanEquip(apparel, pawn)) { continue; }
                if (!pawn.CanReserveAndReach(apparel, PathEndMode.OnCell, Danger.Some)) { continue; }

                best = apparel;
                bestGain = gain;
            }
            _wornScores.Clear();

            return best != null ? JobMaker.MakeJob(JobDefOf.Wear, best) : null;
        }

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
