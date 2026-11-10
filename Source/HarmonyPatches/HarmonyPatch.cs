using HarmonyLib;
using System;
using System.Reflection;
using Verse;
using VVRace.HarmonyPatches;

namespace VVRace
{
    public static class ViviHarmonyPatcher
    {
        // 모드 생성자에서 실행되므로 예외가 밖으로 나가면 VVRaceMod가 등록되지 않아 모드 전체가 깨진다.
        // 패치는 묶음 안에서 TryPatch로 하나씩 격리하고, 그 밖에서 난 예외(속성 패치 등)도 그 묶음만 멈추도록 묶음마다 기록하고 넘어간다.
        public static void PrePatchAll()
        {
            var harmony = new Harmony("rimworld.gguake.vivi");

            PatchGroup(harmony, nameof(ViviRacePatch), ViviRacePatch.Patch);
            PatchGroup(harmony, nameof(ArcanePlantPatch), ArcanePlantPatch.Patch);
            PatchGroup(harmony, nameof(GatherPatch), GatherPatch.Patch);
            PatchGroup(harmony, nameof(BackCompatibilityPatch), BackCompatibilityPatch.Patch);
            PatchGroup(harmony, nameof(EndingCorePatch), EndingCorePatch.Patch);
            PatchGroup(harmony, nameof(ApocryphaPatch), ApocryphaPatch.Patch);
            PatchGroup(harmony, nameof(BeekeepingPatch), BeekeepingPatch.Patch);
            PatchGroup(harmony, "attribute patches", h => h.PatchAll(typeof(ViviHarmonyPatcher).Assembly));
        }

        private static void PatchGroup(Harmony harmony, string group, Action<Harmony> patch)
        {
            try
            {
                patch(harmony);
            }
            catch (Exception ex)
            {
                Log.Error($"[ViViRace] {group} failed; its remaining patches are skipped: {ex}");
            }
        }

        // 게임 업데이트로 대상이 사라졌거나 다른 모드의 transpiler가 교체 메서드를 다시 만들다 실패해도 이 패치 하나만 건너뛴다.
        internal static bool TryPatch(Harmony harmony, string label, MethodBase original,
            HarmonyMethod prefix = null, HarmonyMethod postfix = null, HarmonyMethod transpiler = null, HarmonyMethod finalizer = null)
        {
            if (original == null)
            {
                Log.Error($"[ViViRace] failed to find {label}; this patch is skipped.");
                return false;
            }

            try
            {
                harmony.Patch(original, prefix, postfix, transpiler, finalizer);
                return true;
            }
            catch (Exception ex)
            {
                Log.Error($"[ViViRace] failed to patch {label}: {ex}");
                return false;
            }
        }
    }
}
