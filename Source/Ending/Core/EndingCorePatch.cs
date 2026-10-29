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
    // 엔딩 퀘스트 Harmony 패치 진입점. HarmonyPatch.cs는 이 클래스만 부르고, 다른 엔딩 모듈의 패치 클래스는 여기서 각자 호출한다.
    // 모드 생성자 단계에서 실행되므로 DefOf/DefDatabase를 읽지 않으며, 한 패치가 실패해도 나머지는 계속 적용한다.
    internal static class EndingCorePatch
    {
        internal static void Patch(Harmony harmony)
        {
            // 밀서 퀘스트가 진행 중이면 통신기로 비비 연방에 고대 호박 코어 의뢰를 넣을 수 있다.
            TryPatch(harmony, "FactionDialogMaker.FactionDialogFor",
                AccessTools.Method(typeof(FactionDialogMaker), nameof(FactionDialogMaker.FactionDialogFor)),
                postfixName: nameof(FactionDialogMaker_FactionDialogFor_Postfix));

            // 고대 호박 코어 의뢰 퀘스트를 만드는 동안 바닐라 보상을 코어 1개로 바꾼다(ViviAmberCoreQuestGen).
            TryPatch(harmony, "QuestGen_Rewards.GiveRewards",
                AccessTools.Method(typeof(QuestGen_Rewards), nameof(QuestGen_Rewards.GiveRewards)),
                prefixName: nameof(QuestGen_Rewards_GiveRewards_Prefix));

            // 평화 기간·보스전 동안 엔딩 맵을 노리는 일반 위협(스토리텔러·퀘스트·대기열)을 막는다.
            TryPatch(harmony, "IncidentWorker.CanFireNow",
                AccessTools.Method(typeof(IncidentWorker), nameof(IncidentWorker.CanFireNow)),
                nameof(IncidentWorker_CanFireNow_Postfix));

            // 공성부터 아포크리파 토벌까지 앵커 맵에 습격 비콘 태그를 붙인다(바닐라 우주선 반응로 가동과 같은 스토리텔러 큰 위협).
            TryPatch(harmony, "Map.IncidentTargetTags",
                AccessTools.Method(typeof(Map), nameof(Map.IncidentTargetTags)),
                nameof(Map_IncidentTargetTags_Postfix));

            // 각 엔딩 모듈이 자기 폴더에 두는 패치 클래스(internal static class X { internal static void Patch(Harmony harmony) }).
            // (제단·기믹 모듈은 자체 클래스만으로 동작해 바닐라 패치가 없다)
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

        // 모듈 하나의 패치가 실패해도(예외) 나머지 모듈과 모드 생성자는 계속 진행한다.
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

            // 선택지를 만들다 오류가 나도 통신기 대화는 그대로 연다.
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

            // 맨 끝의 "(통신 종료)" 앞에 넣는다.
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
