using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using Verse;
using Verse.AI.Group;

namespace VVRace
{
    public class Precept_RitualChangeWeather : Precept_Ritual
    {
        public override Window GetRitualBeginWindow(
            TargetInfo targetInfo,
            RitualObligation obligation = null,
            Action onConfirm = null,
            Pawn organizer = null,
            Dictionary<string, Pawn> forcedForRole = null,
            Pawn selectedPawn = null)
        {
            var cannotStartReason = behavior.CanStartRitualNow(targetInfo, this, selectedPawn, forcedForRole);
            if (!string.IsNullOrEmpty(cannotStartReason))
            {
                Messages.Message(cannotStartReason, targetInfo, MessageTypeDefOf.RejectInput, false);
            }

            var extraInfoText = new List<string>();
            if (outcomeEffect != null)
            {
                if (!outcomeEffect.def.extraInfoLines.NullOrEmpty())
                {
                    extraInfoText.AddRange(outcomeEffect.def.extraInfoLines);
                }

                if (!outcomeEffect.def.extraPredictedOutcomeDescriptions.NullOrEmpty())
                {
                    foreach (var outcomeDescription in outcomeEffect.def.extraPredictedOutcomeDescriptions)
                    {
                        extraInfoText.Add(outcomeDescription.Formatted(shortDescOverride ?? def.label));
                    }
                }

                if (attachableOutcomeEffect != null)
                {
                    extraInfoText.Add(attachableOutcomeEffect.DescriptionForRitualValidated(this, targetInfo.Map));
                }
            }

            Dialog_BeginRitual_ChangeWeather dialog = null;

            Dialog_BeginRitual.ActionCallback action = (assignments) =>
            {
                // 의식 시작 시점에 선택한 날씨를 영원꽃에 기록한다. 결과(Apply)에서 소비된다.
                if (targetInfo.Thing is ArcanePlant_Everflower everflower)
                {
                    everflower.SelectedRitualWeather = dialog?.selectedWeather;
                }

                behavior.TryExecuteOn(targetInfo, organizer, this, obligation, assignments, true);
                onConfirm?.Invoke();
                return true;
            };

            Dialog_BeginRitual.PawnFilter filter = (Pawn pawn, bool voluntary, bool allowOtherIdeos) =>
            {
                if (pawn.GetLord() != null ||
                    (pawn.RaceProps.Animal && !behavior.def.roles.Any(r => r.AppliesToPawn(pawn, out string _, targetInfo, skipReason: true))) ||
                    pawn.IsSubhuman)
                {
                    return false;
                }

                if (!ritualOnlyForIdeoMembers || def.allowSpectatorsFromOtherIdeos || pawn.Ideo == ideo || !voluntary || allowOtherIdeos || pawn.IsPrisonerOfColony || pawn.RaceProps.Animal)
                {
                    return true;
                }

                return !forcedForRole.NullOrEmpty() && forcedForRole.ContainsValue(pawn);
            };

            var okButtonText = "Begin".Translate();
            List<Pawn> requiredPawns = null;
            if (organizer != null)
            {
                requiredPawns = new List<Pawn> { organizer };
            }

            dialog = new Dialog_BeginRitual_ChangeWeather(
                Label.CapitalizeFirst(),
                this,
                targetInfo,
                targetInfo.Map,
                action,
                organizer,
                obligation,
                filter,
                okButtonText,
                requiredPawns,
                forcedForRole,
                extraInfoText: extraInfoText,
                selectedPawn: selectedPawn);

            return dialog;
        }
    }
}
