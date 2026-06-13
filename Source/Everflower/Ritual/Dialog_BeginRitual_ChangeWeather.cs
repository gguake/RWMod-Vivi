using RimWorld;
using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace VVRace
{
    public class Dialog_BeginRitual_ChangeWeather : Dialog_BeginRitual
    {
        // 플레이어가 선택한 날씨. 선택 전에는 null.
        public EverflowerWeatherChoice? selectedWeather;

        private const float HeaderHeight = 28f;
        private const float RowHeight = 28f;

        public Dialog_BeginRitual_ChangeWeather(
            string ritualLabel,
            Precept_Ritual ritual,
            TargetInfo target,
            Map map,
            ActionCallback action,
            Pawn organizer,
            RitualObligation obligation,
            PawnFilter filter = null,
            string okButtonText = null,
            List<Pawn> requiredPawns = null,
            Dictionary<string, Pawn> forcedForRole = null,
            RitualOutcomeEffectDef outcome = null,
            List<string> extraInfoText = null,
            Pawn selectedPawn = null)
            : base(ritualLabel, ritual, target, map, action, organizer, obligation, filter, okButtonText, requiredPawns, forcedForRole, outcome, extraInfoText, selectedPawn)
        {
        }

        protected override IEnumerable<string> BlockingIssues()
        {
            foreach (var issue in base.BlockingIssues())
            {
                yield return issue;
            }

            // 날씨를 선택하지 않으면 진행 불가.
            if (!selectedWeather.HasValue)
            {
                yield return LocalizeString_Dialog.VV_DialogChangeWeatherMustSelect.Translate();
            }
        }

        public override void DoRightColumn(ref RectDivider layout)
        {
            var totalHeight = HeaderHeight + RowHeight * EverflowerWeatherChoiceUtility.AllChoices.Length;
            var rect = layout.NewRow(totalHeight).Rect;

            var headerRect = new Rect(rect.x, rect.y, rect.width, HeaderHeight);
            using (new TextBlock(GameFont.Small, TextAnchor.MiddleLeft))
            {
                Widgets.Label(headerRect, LocalizeString_Dialog.VV_DialogChangeWeatherSelectLabel.Translate());
            }

            var curY = rect.y + HeaderHeight;
            foreach (var choice in EverflowerWeatherChoiceUtility.AllChoices)
            {
                var rowRect = new Rect(rect.x + 8f, curY, Mathf.Min(rect.width - 8f, 360f), RowHeight);
                if (Widgets.RadioButtonLabeled(rowRect, EverflowerWeatherChoiceUtility.Label(choice), selectedWeather == choice))
                {
                    selectedWeather = choice;
                }

                if (choice == EverflowerWeatherChoice.Rain)
                {
                    TooltipHandler.TipRegion(rowRect, LocalizeString_Dialog.VV_DialogChangeWeatherRainDesc.Translate());
                }

                curY += RowHeight;
            }

            layout.NewRow(10f);
            base.DoRightColumn(ref layout);
        }
    }
}
