using RimWorld;
using System.Collections.Generic;
using System.Linq;
using Verse;
using Verse.AI.Group;

namespace VVRace
{
    // 선택 항목이 있는 영원꽃 의식(날씨 변화, 꽃의 대지)이 전용 의식 창을 만들 때 바닐라 Precept_Ritual.GetRitualBeginWindow와
    // 같은 안내 문구·참여자 필터를 쓰도록 공통 부분을 모은다.
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

        public static List<Pawn> RequiredPawns(Pawn organizer)
        {
            return organizer != null ? new List<Pawn> { organizer } : null;
        }
    }
}
