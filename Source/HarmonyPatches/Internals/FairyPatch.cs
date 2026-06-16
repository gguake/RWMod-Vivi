using HarmonyLib;
using RimWorld;
using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace VVRace
{
    // 경호 대상 Pawn을 선택하면 "보호 해제" Gizmo를 추가한다.
    internal static class FairyPatch
    {
        internal static void Patch(Harmony harmony)
        {
            harmony.Patch(
                original: AccessTools.Method(typeof(Pawn), nameof(Pawn.GetGizmos)),
                postfix: new HarmonyMethod(typeof(FairyPatch), nameof(Pawn_GetGizmos_Postfix)));
        }

        private static IEnumerable<Gizmo> Pawn_GetGizmos_Postfix(IEnumerable<Gizmo> __result, Pawn __instance)
        {
            foreach (var gizmo in __result)
            {
                yield return gizmo;
            }

            if (__instance == null || __instance.health == null) { yield break; }
            if (__instance.Faction != Faction.OfPlayer) { yield break; }

            var hediff = __instance.health.hediffSet.GetFirstHediffOfDef(VVHediffDefOf.VV_FairyGuarded) as Hediff_FairyGuarded;
            if (hediff == null || hediff.ownerVivi == null) { yield break; }

            var pawn = __instance;
            var guardHediff = hediff;
            yield return new Command_Action
            {
                defaultLabel = LocalizeString_Etc.VV_Command_ReleaseGuard.Translate(),
                defaultDesc = LocalizeString_Etc.VV_Command_ReleaseGuardDesc.Translate(),
                icon = ContentFinder<Texture2D>.Get("Things/Mote/VV_Fairy/VV_Fairy_south", reportFailure: false),
                action = () =>
                {
                    var ctrl = guardHediff.ownerVivi != null ? guardHediff.ownerVivi.GetComp<CompViviFairyController>() : null;
                    if (ctrl != null)
                    {
                        ctrl.EndSession(guardHediff.sessionId);
                    }
                    else if (pawn.health != null)
                    {
                        pawn.health.RemoveHediff(guardHediff);
                    }
                }
            };
        }
    }
}
