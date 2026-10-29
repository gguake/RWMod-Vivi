using HarmonyLib;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Verse;

namespace VVRace
{
    // 엔딩 퀘스트 Harmony 패치 진입점. HarmonyPatch.cs는 이 클래스만 부르고, 다른 엔딩 모듈의 패치 클래스는 이름으로 찾아 각자 호출한다.
    // 모드 생성자 단계에서 실행되므로 DefOf/DefDatabase를 읽지 않으며, 한 패치가 실패해도 나머지는 계속 적용한다.
    internal static class EndingCorePatch
    {
        // 각 엔딩 모듈이 자기 폴더에 두는 패치 클래스(internal static class X { internal static void Patch(Harmony harmony) }).
        // 이 어셈블리에서 단순 이름으로 찾는다(네임스페이스 무시). 없으면 경고 후 건너뛴다.
        private static readonly string[] ModulePatchTypeNames =
        {
            "AltarPatch",
            "EndingRitualPatch",
            "ApocryphaPatch",
            "ApocryphaGimmickPatch",
        };

        internal static void Patch(Harmony harmony)
        {
            // 〈고대 제단 복원〉 연구는 연방의 서신을 받기 전까지 "선행 연구 미완료"로 잠근다.
            TryPatch(harmony, "ResearchProjectDef.PrerequisitesCompleted",
                AccessTools.PropertyGetter(typeof(ResearchProjectDef), nameof(ResearchProjectDef.PrerequisitesCompleted)),
                nameof(ResearchProjectDef_PrerequisitesCompleted_Postfix));

            // 평화 기간·보스전 동안 엔딩 맵을 노리는 일반 위협(스토리텔러·퀘스트·대기열)을 막는다.
            TryPatch(harmony, "IncidentWorker.CanFireNow",
                AccessTools.Method(typeof(IncidentWorker), nameof(IncidentWorker.CanFireNow)),
                nameof(IncidentWorker_CanFireNow_Postfix));

            // 공성부터 아포크리파 토벌까지 앵커 맵에 습격 비콘 태그를 붙인다(바닐라 우주선 반응로 가동과 같은 스토리텔러 큰 위협).
            TryPatch(harmony, "Map.IncidentTargetTags",
                AccessTools.Method(typeof(Map), nameof(Map.IncidentTargetTags)),
                nameof(Map_IncidentTargetTags_Postfix));

            foreach (var typeName in ModulePatchTypeNames)
            {
                PatchModule(harmony, typeName);
            }
        }

        private static void TryPatch(Harmony harmony, string label, MethodBase original, string postfixName)
        {
            if (original == null)
            {
                Log.Error($"[ViViRace] ending patch: failed to find {label}; this patch is skipped.");
                return;
            }

            try
            {
                harmony.Patch(original, postfix: new HarmonyMethod(typeof(EndingCorePatch), postfixName));
            }
            catch (Exception ex)
            {
                Log.Error($"[ViViRace] ending patch: failed to patch {label}: {ex}");
            }
        }

        private static void PatchModule(Harmony harmony, string typeName)
        {
            try
            {
                var type = AccessTools.TypeByName("VVRace." + typeName)
                    ?? typeof(EndingCorePatch).Assembly.GetTypes().FirstOrDefault(t => t.Name == typeName);
                if (type == null)
                {
                    Log.Warning($"[ViViRace] ending patch: module patch class {typeName} not found; skipped.");
                    return;
                }

                var method = AccessTools.Method(type, "Patch", new[] { typeof(Harmony) });
                if (method == null || !method.IsStatic)
                {
                    Log.Error($"[ViViRace] ending patch: {typeName} has no static Patch(Harmony) method.");
                    return;
                }

                method.Invoke(null, new object[] { harmony });
            }
            catch (Exception ex)
            {
                var inner = (ex as TargetInvocationException)?.InnerException ?? ex;
                Log.Error($"[ViViRace] ending patch: {typeName}.Patch failed: {inner}");
            }
        }

        private static void ResearchProjectDef_PrerequisitesCompleted_Postfix(ResearchProjectDef __instance, ref bool __result)
        {
            if (!__result || __instance == null || __instance != EndingCoreDefOf.VV_AncientAltarRestoration) { return; }

            var quest = ViviEndingQuest.Current;
            if (quest == null || !quest.IsAltarUnlocked)
            {
                __result = false;
            }
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
