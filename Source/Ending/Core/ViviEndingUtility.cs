using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace VVRace
{
    public static class ViviEndingUtility
    {
        private const int GoldenBloomFadeTicks = 30000;

        private static readonly FieldInfo QueuedIncidentsField = typeof(IncidentQueue).GetField("queuedIncidents", BindingFlags.Instance | BindingFlags.NonPublic);

        public static ArcanePlant_Everflower FindEndingCandidate()
        {
            var everflowerDef = VVThingDefOf.VV_Everflower;
            if (everflowerDef == null) { return null; }

            ArcanePlant_Everflower best = null;
            var bestScore = int.MinValue;
            foreach (var map in Find.Maps)
            {
                var things = map.listerThings.ThingsOfDef(everflowerDef);
                for (int i = 0; i < things.Count; ++i)
                {
                    if (!(things[i] is ArcanePlant_Everflower everflower) || everflower.Faction != Faction.OfPlayer) { continue; }

                    var comp = everflower.EverflowerComp;
                    if (comp == null) { continue; }

                    var score = comp.AttunementLevel * 100
                        + (comp.IsEnshrined ? 20 : 0)
                        + (map.IsPlayerHome ? 10 : 0)
                        + (comp.LinkedPawns.Any() ? 1 : 0);

                    if (score > bestScore)
                    {
                        best = everflower;
                        bestScore = score;
                    }
                }
            }

            return best;
        }

        public static Faction FindFederation(bool requireNonHostile)
        {
            var federationDef = VVFactionDefOf.VV_ViviFederation;
            if (federationDef == null) { return null; }

            Faction hostile = null;
            var factions = Find.FactionManager.AllFactionsListForReading;
            for (int i = 0; i < factions.Count; ++i)
            {
                var faction = factions[i];
                if (faction.def != federationDef || faction.defeated) { continue; }

                if (!faction.HostileTo(Faction.OfPlayer))
                {
                    return faction;
                }

                if (hostile == null)
                {
                    hostile = faction;
                }
            }

            return requireNonHostile ? null : hostile;
        }

        public static Pawn ResonatorOf(ArcanePlant_Everflower everflower)
        {
            return everflower?.EverflowerComp?.LinkedPawns?.FirstOrDefault();
        }

        public static Building_EverflowerAltar AltarOf(ArcanePlant_Everflower everflower)
        {
            var altar = everflower != null && !everflower.Destroyed ? everflower.EverflowerComp?.EnshrinedAltar : null;
            if (altar != null) { return altar; }

            var map = everflower?.MapHeld;
            return map != null ? Building_EverflowerAltar.FindOnMap(map) : null;
        }

        public static void GrantQueenPowers(Pawn pawn)
        {
            if (pawn?.health?.hediffSet == null || pawn.health.hediffSet.HasHediff(VVHediffDefOf.VV_EverflowerQueen)) { return; }

            pawn.health.AddHediff(VVHediffDefOf.VV_EverflowerQueen);
        }

        public static TaggedString TranslateWithResonator(string key, Pawn resonator, params NamedArgument[] args)
        {
            if (resonator != null)
            {
                var allArgs = new NamedArgument[args.Length + 1];
                allArgs[0] = resonator.Named("PAWN");
                args.CopyTo(allArgs, 1);
                return key.Translate(allArgs);
            }

            var raw = key.Translate().RawText.Replace("{PAWN_labelShort}", LocalizeString_Etc.VV_Ending_ResonatorFallback.Translate().RawText);
            return raw.Formatted(args);
        }

        public static bool IsThreatCategory(IncidentCategoryDef category)
        {
            return category != null
                && (category == IncidentCategoryDefOf.ThreatBig || category == IncidentCategoryDefOf.ThreatSmall || category == IncidentCategoryDefOf.DeepDrillInfestation);
        }

        public static int PurgeQueuedThreats(Map map)
        {
            if (QueuedIncidentsField == null)
            {
                Log.ErrorOnce("[ViViRace] cannot find IncidentQueue.queuedIncidents; queued threats are not purged during the ending calm.", 0x5645_4E10);
                return 0;
            }

            if (!(QueuedIncidentsField.GetValue(Find.Storyteller.incidentQueue) is List<QueuedIncident> queued)) { return 0; }

            return queued.RemoveAll(v => v?.FiringIncident?.parms?.target == map && IsThreatCategory(v.FiringIncident.def?.category));
        }

        public static int VanishWildTitanicHornets(Map map)
        {
            return VanishTitanicHornets(map, EffecterDefOf.Skip_EntryNoDelay, pawn => !(pawn.GetLord()?.LordJob is LordJob_ApocryphaEscort));
        }

        public static int VanishAllTitanicHornets(Map map)
        {
            return VanishTitanicHornets(map, VVEffecterDefOf.VV_Effecter_ApocryphaVanish ?? EffecterDefOf.Skip_EntryNoDelay, null);
        }

        private static int VanishTitanicHornets(Map map, EffecterDef vanishEffect, Predicate<Pawn> filter)
        {
            var hornetDef = VVThingDefOf.VV_TitanicHornet;
            if (hornetDef == null || map == null) { return 0; }

            var vanished = 0;
            foreach (var pawn in map.mapPawns.AllPawnsSpawned.ToList())
            {
                if (pawn.def != hornetDef || pawn.Destroyed || pawn.Faction == Faction.OfPlayer) { continue; }
                if (filter != null && !filter(pawn)) { continue; }

                vanishEffect.Spawn(pawn.Position, map).Cleanup();
                pawn.Destroy(DestroyMode.Vanish);
                vanished++;
            }

            return vanished;
        }

        // 착륙 중인 드롭포드의 습격대원도 이미 로드에 속하므로 함께 옮겨 포드가 열리자마자 철수하게 한다.
        public static int MakeRaidersRetreat(Map map)
        {
            var retreated = 0;
            foreach (var lord in map.lordManager.lords.ToList())
            {
                var faction = lord.faction;
                if (faction == null || faction == Faction.OfPlayer || !faction.HostileTo(Faction.OfPlayer)) { continue; }
                if (!IsAssaultLordJob(lord.LordJob)) { continue; }

                var leavers = new List<Pawn>();
                foreach (var pawn in lord.ownedPawns.ToList())
                {
                    if (pawn.Dead || pawn.Downed || (pawn.Spawned && pawn.Map != map)) { continue; }

                    lord.Notify_PawnLost(pawn, PawnLostCondition.ForcedToJoinOtherLord);
                    if (pawn.GetLord() == null)
                    {
                        leavers.Add(pawn);
                    }
                }

                if (leavers.Count == 0) { continue; }

                LordMaker.MakeNewLord(faction, new LordJob_ExitMapBest(LocomotionUrgency.Jog, canDig: true, canDefendSelf: true), map, leavers);
                retreated += leavers.Count;
            }

            return retreated;
        }

        private static bool IsAssaultLordJob(LordJob lordJob)
        {
            return lordJob is LordJob_AssaultColony
                || lordJob is LordJob_Siege
                || lordJob is LordJob_StageThenAttack
                || lordJob is LordJob_AssaultThings
                || lordJob is LordJob_SleepThenAssaultColony
                || lordJob is LordJob_BossgroupAssaultColony;
        }

        public static void EndOminousSilence()
        {
            foreach (var map in Find.Maps)
            {
                EndOminousSilence(map);
            }
        }

        public static void EndOminousSilence(Map map)
        {
            var conditionDef = VVGameConditionDefOf.VV_OminousSilence;
            if (conditionDef == null || map == null) { return; }

            map.gameConditionManager.GetActiveCondition(conditionDef)?.End();
        }

        // 의식으로 부른 꽃안개가 먼저 올 수 있으므로 첫 항목만 보지 않고 모든 컨디션을 검사한다.
        public static void FadeGoldenBloom()
        {
            foreach (var map in Find.Maps)
            {
                foreach (var condition in EndingGoldenBlooms(map))
                {
                    condition.fromEnding = false;
                    condition.Permanent = false;
                    condition.Duration = condition.TicksPassed + GoldenBloomFadeTicks;
                }
            }
        }

        public static void EndGoldenBloom()
        {
            foreach (var map in Find.Maps)
            {
                EndGoldenBloom(map);
            }
        }

        public static void EndGoldenBloom(Map map)
        {
            foreach (var condition in EndingGoldenBlooms(map))
            {
                condition.End();
            }
        }

        // 컨디션을 해제하면 목록이 바뀌므로 엔딩 꽃안개 목록의 복사본을 순회한다.
        private static List<GameCondition_GoldenBloom> EndingGoldenBlooms(Map map)
        {
            var result = new List<GameCondition_GoldenBloom>();
            var conditions = map?.gameConditionManager.ActiveConditions;
            if (conditions == null) { return result; }

            for (int i = 0; i < conditions.Count; ++i)
            {
                if (conditions[i] is GameCondition_GoldenBloom condition && condition.fromEnding)
                {
                    result.Add(condition);
                }
            }

            return result;
        }

        public static void CleanupGimmicks()
        {
            foreach (var map in Find.Maps.ToList())
            {
                try
                {
                    ApocryphaGimmicks.CleanupAll(map);
                }
                catch (Exception ex)
                {
                    Log.Error($"[ViViRace] failed to clean up Asphodel gimmicks on {map}: {ex}");
                }
            }
        }

        public static void VanishBoss(Pawn boss)
        {
            if (boss == null || boss.Destroyed || !boss.Spawned) { return; }

            var map = boss.Map;
            VanishEscorts(boss);

            var vanishEffect = VVEffecterDefOf.VV_Effecter_ApocryphaVanish ?? EffecterDefOf.Skip_EntryNoDelay;
            vanishEffect.Spawn(boss.Position, map, 2.5f).Cleanup();
            boss.Destroy(DestroyMode.Vanish);
        }

        public static void VanishEscorts(Pawn boss)
        {
            var vanishEffect = VVEffecterDefOf.VV_Effecter_ApocryphaVanish ?? EffecterDefOf.Skip_EntryNoDelay;
            foreach (var hornet in ApocryphaUtility.GetEscorts(boss))
            {
                if (hornet == null || hornet.Destroyed) { continue; }

                if (hornet.Spawned)
                {
                    vanishEffect.Spawn(hornet.Position, hornet.Map).Cleanup();
                }

                hornet.Destroy(DestroyMode.Vanish);
            }
        }

        public static void RunStep(string label, Action action)
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                Log.Error($"[ViViRace] ending step '{label}' failed: {ex}");
            }
        }
    }
}
