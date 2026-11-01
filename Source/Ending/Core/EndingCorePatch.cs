using HarmonyLib;
using RimWorld;
using RimWorld.QuestGen;
using System;
using System.Collections.Generic;
using System.Reflection;
using Verse;
using Verse.Grammar;

namespace VVRace
{
    // 모드 생성자에서 실행되므로 DefOf나 DefDatabase를 읽지 않는다. 패치 하나가 실패해도 나머지는 계속 적용한다.
    internal static class EndingCorePatch
    {
        internal static void Patch(Harmony harmony)
        {
            TryPatch(harmony, "FactionDialogMaker.FactionDialogFor",
                AccessTools.Method(typeof(FactionDialogMaker), nameof(FactionDialogMaker.FactionDialogFor)),
                postfixName: nameof(FactionDialogMaker_FactionDialogFor_Postfix));

            TryPatch(harmony, "QuestGen_Rewards.GiveRewards",
                AccessTools.Method(typeof(QuestGen_Rewards), nameof(QuestGen_Rewards.GiveRewards)),
                prefixName: nameof(QuestGen_Rewards_GiveRewards_Prefix));

            TryPatch(harmony, "IncidentWorker.CanFireNow",
                AccessTools.Method(typeof(IncidentWorker), nameof(IncidentWorker.CanFireNow)),
                nameof(IncidentWorker_CanFireNow_Postfix));

            TryPatch(harmony, "Map.IncidentTargetTags",
                AccessTools.Method(typeof(Map), nameof(Map.IncidentTargetTags)),
                nameof(Map_IncidentTargetTags_Postfix));

            PatchModule(harmony, nameof(EndingRitualPatch), EndingRitualPatch.Patch);
            PatchModule(harmony, nameof(ApocryphaPatch), ApocryphaPatch.Patch);
        }

        private static void TryPatch(Harmony harmony, string label, MethodBase original, string postfixName = null, string prefixName = null)
        {
            if (original == null)
            {
                Log.Error($"[ViViRace] ending patch: failed to find {label}; this patch is skipped.");
                return;
            }

            try
            {
                harmony.Patch(original,
                    prefix: prefixName != null ? new HarmonyMethod(typeof(EndingCorePatch), prefixName) : null,
                    postfix: postfixName != null ? new HarmonyMethod(typeof(EndingCorePatch), postfixName) : null);
            }
            catch (Exception ex)
            {
                Log.Error($"[ViViRace] ending patch: failed to patch {label}: {ex}");
            }
        }

        private static void PatchModule(Harmony harmony, string moduleName, Action<Harmony> patch)
        {
            try
            {
                patch(harmony);
            }
            catch (Exception ex)
            {
                Log.Error($"[ViViRace] ending patch: {moduleName}.Patch failed: {ex}");
            }
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
                Log.ErrorOnce($"[ViViRace] failed to make the amber core comms option: {ex}", 0x5645_4E30);
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
