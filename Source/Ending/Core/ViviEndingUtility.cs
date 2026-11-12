using HarmonyLib;
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

        internal const int ErrorKeyQuestTick = 0x5645_4E00;
        internal const int ErrorKeyQueuedThreats = 0x5645_4E10;
        internal const int ErrorKeyAltarPulse = 0x5645_4E20;
        internal const int ErrorKeyAssimilationTick = 0x5645_4E30;
        internal const int ErrorKeyAmberCommsOption = 0x5645_4E40;
        internal const int ErrorKeyQueenDeathIntercept = 0x5645_4E50;

        private static readonly FieldInfo QueuedIncidentsField = AccessTools.Field(typeof(IncidentQueue), "queuedIncidents");

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

        public static void GrantQueenPowers(Pawn pawn, ArcanePlant_Everflower everflower)
        {
            if (pawn?.health?.hediffSet == null || pawn.health.hediffSet.HasHediff(VVHediffDefOf.VV_EverflowerQueen)) { return; }

            var hediff = (Hediff_EverflowerQueen)HediffMaker.MakeHediff(VVHediffDefOf.VV_EverflowerQueen, pawn);
            hediff.everflower = everflower;
            pawn.health.AddHediff(hediff);
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
                Log.ErrorOnce("[ViViRace] cannot find IncidentQueue.queuedIncidents; queued threats are not purged during the ending calm.", ErrorKeyQueuedThreats);
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

        public static void FadeGoldenBloom()
        {
            foreach (var map in Find.Maps)
            {
                foreach (var condition in GoldenBloomUtility.EndingConditions(map).ToList())
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
            foreach (var condition in GoldenBloomUtility.EndingConditions(map).ToList())
            {
                condition.End();
            }
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
