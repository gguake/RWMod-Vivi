using HarmonyLib;
using RimWorld;
using RimWorld.QuestGen;
using System;
using System.Collections.Generic;
using Verse;
using Verse.Grammar;

namespace VVRace
{
    // 모드 생성자에서 실행되므로 DefOf나 DefDatabase를 읽지 않는다. 패치 하나가 실패해도 나머지는 계속 적용한다.
    internal static class EndingCorePatch
    {
        internal static void Patch(Harmony harmony)
        {
            ViviHarmonyPatcher.TryPatch(harmony, "FactionDialogMaker.FactionDialogFor (amber core comms option)",
                AccessTools.Method(typeof(FactionDialogMaker), nameof(FactionDialogMaker.FactionDialogFor)),
                postfix: new HarmonyMethod(typeof(EndingCorePatch), nameof(FactionDialogMaker_FactionDialogFor_Postfix)));

            ViviHarmonyPatcher.TryPatch(harmony, "QuestGen_Rewards.GiveRewards (amber core quest reward)",
                AccessTools.Method(typeof(QuestGen_Rewards), nameof(QuestGen_Rewards.GiveRewards)),
                prefix: new HarmonyMethod(typeof(EndingCorePatch), nameof(QuestGen_Rewards_GiveRewards_Prefix)));

            ViviHarmonyPatcher.TryPatch(harmony, "IncidentWorker.CanFireNow (ending threat suppression)",
                AccessTools.Method(typeof(IncidentWorker), nameof(IncidentWorker.CanFireNow)),
                postfix: new HarmonyMethod(typeof(EndingCorePatch), nameof(IncidentWorker_CanFireNow_Postfix)));

            ViviHarmonyPatcher.TryPatch(harmony, "Map.IncidentTargetTags (ending raid beacon)",
                AccessTools.Method(typeof(Map), nameof(Map.IncidentTargetTags)),
                postfix: new HarmonyMethod(typeof(EndingCorePatch), nameof(Map_IncidentTargetTags_Postfix)));

            ViviHarmonyPatcher.TryPatch(harmony, "Fire.TrySpread (golden bloom fire containment)",
                AccessTools.Method(typeof(Fire), "TrySpread"),
                prefix: new HarmonyMethod(typeof(EndingCorePatch), nameof(Fire_TrySpread_Prefix)));
        }

        private static void FactionDialogMaker_FactionDialogFor_Postfix(Pawn negotiator, Faction faction, DiaNode __result)
        {
            if (__result?.options == null) { return; }

            DiaOption option;
            try
            {
                option = AmberCoreCommsOption.Make(negotiator, faction);
            }
            catch (Exception ex)
            {
                Log.ErrorOnce($"[ViViRace] failed to make the amber core comms option: {ex}", ViviEndingUtility.ErrorKeyAmberCommsOption);
                return;
            }

            if (option == null) { return; }

            var options = __result.options;
            options.Insert(Math.Max(0, options.Count - 1), option);
        }

        private static bool QuestGen_Rewards_GiveRewards_Prefix(Quest quest, RewardsGeneratorParams parms, string inSignal, string customLetterLabel,
            string customLetterText, RulePack customLetterLabelRules, RulePack customLetterTextRules, ref QuestPart_Choice __result)
        {
            if (!ViviAmberCoreQuestGen.Generating) { return true; }

            if (!ViviAmberCoreQuestGen.TryGiveCoreReward(quest, parms, inSignal, customLetterLabel, customLetterText, customLetterLabelRules, customLetterTextRules, out var choice))
            {
                return true;
            }

            __result = choice;
            return false;
        }

        private static void Map_IncidentTargetTags_Postfix(Map __instance, ref IEnumerable<IncidentTargetTagDef> __result)
        {
            var quest = ViviEndingQuest.Current;
            if (quest == null || !quest.IsRaidBeaconActive(__instance)) { return; }

            __result = WithRaidBeacon(__result);
        }

        private static IEnumerable<IncidentTargetTagDef> WithRaidBeacon(IEnumerable<IncidentTargetTagDef> tags)
        {
            var hasBeacon = false;
            foreach (var tag in tags)
            {
                hasBeacon |= tag == IncidentTargetTagDefOf.Map_RaidBeacon;
                yield return tag;
            }

            if (!hasBeacon)
            {
                yield return IncidentTargetTagDefOf.Map_RaidBeacon;
            }
        }

        // 황금빛 꽃안개 중에는 불이 제자리에서만 타고 옆 칸이나 불똥으로 번지지 않는다.
        private static bool Fire_TrySpread_Prefix(Fire __instance)
        {
            return !GoldenBloomUtility.IsActive(__instance.Map);
        }

        private static void IncidentWorker_CanFireNow_Postfix(IncidentWorker __instance, IncidentParms parms, ref bool __result)
        {
            if (!__result) { return; }
            if (!(parms?.target is Map map)) { return; }
            if (!ViviEndingUtility.IsThreatCategory(__instance?.def?.category)) { return; }

            var quest = ViviEndingQuest.Current;
            if (quest != null && quest.ShouldSuppressThreats(map))
            {
                __result = false;
            }
        }
    }
}
