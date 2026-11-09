using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using Verse;
using Verse.AI.Group;

namespace VVRace
{
    public static class EverflowerRitualDialogUtility
    {
        public static void NotifyCannotStart(Precept_Ritual ritual, TargetInfo target, Pawn selectedPawn, Dictionary<string, Pawn> forcedForRole)
        {
            var reason = ritual.behavior.CanStartRitualNow(target, ritual, selectedPawn, forcedForRole);
            if (!string.IsNullOrEmpty(reason))
            {
                Messages.Message(reason, target, MessageTypeDefOf.RejectInput, false);
            }
        }

        // 시작 버튼 콜백: 대화창에서 고른 값을 영원꽃에 저장한 뒤 의식을 시작한다.
        public static Dialog_BeginRitual.ActionCallback MakeBeginAction(Precept_Ritual ritual, TargetInfo target, Pawn organizer, RitualObligation obligation, Action onConfirm, Action<ArcanePlant_Everflower> applySelection)
        {
            return assignments =>
            {
                if (target.Thing is ArcanePlant_Everflower everflower)
                {
                    applySelection(everflower);
                }

                ritual.behavior.TryExecuteOn(target, organizer, ritual, obligation, assignments, true);
                onConfirm?.Invoke();
                return true;
            };
        }

        public static List<string> ExtraInfoText(Precept_Ritual ritual, TargetInfo target)
        {
            var extraInfoText = new List<string>();
            var outcomeEffect = ritual.outcomeEffect;
            if (outcomeEffect == null) { return extraInfoText; }

            if (!outcomeEffect.def.extraInfoLines.NullOrEmpty())
            {
                extraInfoText.AddRange(outcomeEffect.def.extraInfoLines);
            }

            if (!outcomeEffect.def.extraPredictedOutcomeDescriptions.NullOrEmpty())
            {
                foreach (var outcomeDescription in outcomeEffect.def.extraPredictedOutcomeDescriptions)
                {
                    extraInfoText.Add(outcomeDescription.Formatted(ritual.shortDescOverride ?? ritual.def.label));
                }
            }

            if (ritual.attachableOutcomeEffect != null)
            {
                extraInfoText.Add(ritual.attachableOutcomeEffect.DescriptionForRitualValidated(ritual, target.Map));
            }

            return extraInfoText;
        }

        public static Dialog_BeginRitual.PawnFilter PawnFilter(Precept_Ritual ritual, TargetInfo target, Dictionary<string, Pawn> forcedForRole)
        {
            return (pawn, voluntary, allowOtherIdeos) =>
            {
                if (pawn.GetLord() != null ||
                    (pawn.RaceProps.Animal && !ritual.behavior.def.roles.Any(r => r.AppliesToPawn(pawn, out string _, target, skipReason: true))) ||
                    pawn.IsSubhuman)
                {
                    return false;
                }

                if (!ritual.ritualOnlyForIdeoMembers || ritual.def.allowSpectatorsFromOtherIdeos || pawn.Ideo == ritual.ideo || !voluntary || allowOtherIdeos || pawn.IsPrisonerOfColony || pawn.RaceProps.Animal)
                {
                    return true;
                }

                return !forcedForRole.NullOrEmpty() && forcedForRole.ContainsValue(pawn);
            };
        }
    }
}
