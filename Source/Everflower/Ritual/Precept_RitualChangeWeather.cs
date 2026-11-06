using RimWorld;
using System;
using System.Collections.Generic;
using Verse;

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
            EverflowerRitualDialogUtility.NotifyCannotStart(this, targetInfo, selectedPawn, forcedForRole);

            Dialog_BeginRitual_ChangeWeather dialog = null;

            Dialog_BeginRitual.ActionCallback action = (assignments) =>
            {
                if (targetInfo.Thing is ArcanePlant_Everflower everflower)
                {
                    everflower.SelectedRitualWeather = dialog?.selectedWeather;
                }

                behavior.TryExecuteOn(targetInfo, organizer, this, obligation, assignments, true);
                onConfirm?.Invoke();
                return true;
            };

            dialog = new Dialog_BeginRitual_ChangeWeather(
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

            return dialog;
        }
    }
}
