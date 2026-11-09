using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;

namespace VVRace
{
    public class Precept_RitualFlowerLand : Precept_Ritual
    {
        private float Radius => (outcomeEffect?.def as RitualOutcomeEffectDef_FlowerLand)?.radius ?? 0f;

        public override Window GetRitualBeginWindow(
            TargetInfo targetInfo,
            RitualObligation obligation = null,
            Action onConfirm = null,
            Pawn organizer = null,
            Dictionary<string, Pawn> forcedForRole = null,
            Pawn selectedPawn = null)
        {
            var map = targetInfo.Map;
            if (map == null) { return null; }

            var icon = ContentFinder<Texture2D>.Get(def.iconPath, reportFailure: false);
            Find.Targeter.BeginTargeting(
                TargetingParameters.ForCell(),
                action: target =>
                {
                    // 검증에 실패한 클릭은 action이 호출되지 않으므로 거부 메시지가 필요한 검사는 여기서 한다.
                    if (!FlowerLandUtility.ConvertibleCells(map, target.Cell, Radius).Any())
                    {
                        Messages.Message(LocalizeString_Message.VV_Message_FlowerLandNoGround.Translate(), new LookTargets(target.Cell, map), MessageTypeDefOf.RejectInput, false);
                        return;
                    }

                    Find.WindowStack.Add(MakeDialog(targetInfo, target.Cell, obligation, onConfirm, organizer, forcedForRole, selectedPawn));
                },
                highlightAction: target => FlowerLandUtility.DrawPreview(map, target.Cell, Radius),
                targetValidator: target => target.Cell.InBounds(map) && !target.Cell.Fogged(map),
                mouseAttachment: icon);

            return null;
        }

        private Window MakeDialog(TargetInfo targetInfo, IntVec3 cell, RitualObligation obligation, Action onConfirm, Pawn organizer, Dictionary<string, Pawn> forcedForRole, Pawn selectedPawn)
        {
            EverflowerRitualDialogUtility.NotifyCannotStart(this, targetInfo, selectedPawn, forcedForRole);

            var action = EverflowerRitualDialogUtility.MakeBeginAction(this, targetInfo, organizer, obligation, onConfirm,
                everflower => everflower.SelectedFlowerLandCell = cell);

            return new Dialog_BeginRitual_FlowerLand(
                cell,
                Radius,
                Label.CapitalizeFirst(),
                this,
                targetInfo,
                targetInfo.Map,
                action,
                organizer,
                obligation,
                EverflowerRitualDialogUtility.PawnFilter(this, targetInfo, forcedForRole),
                "Begin".Translate(),
                organizer != null ? new List<Pawn> { organizer } : null,
                forcedForRole,
                extraInfoText: EverflowerRitualDialogUtility.ExtraInfoText(this, targetInfo),
                selectedPawn: selectedPawn);
        }
    }
}
