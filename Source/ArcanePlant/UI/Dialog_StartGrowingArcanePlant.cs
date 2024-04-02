using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace VVRace
{
    public class Dialog_StartGrowingArcanePlant : Window
    {
        public override Vector2 InitialSize => new Vector2(Mathf.Min(650f, UI.screenWidth), 700f);

        private Building_ArcanePlantFarm _billGiver;
        private Action<GrowingArcanePlantBill> _callback;

        private List<ThingDef> _plantCandidates;
        private Vector2 _scrollPosition;

        private ThingDef _currentSelected;

        private const float OptionWidth = 300f;
        private const float OptionHeight = 50f;

        public Dialog_StartGrowingArcanePlant(
            Building_ArcanePlantFarm billGiver, 
            Action<GrowingArcanePlantBill> callback)
        {
            absorbInputAroundWindow = true;
            preventSave = true;
            forcePause = true;
            closeOnAccept = false;
            doCloseX = true;

            _billGiver = billGiver;
            _callback = callback;

            _plantCandidates = new List<ThingDef>();
            foreach (var thingDef in DefDatabase<ThingDef>.AllDefsListForReading)
            {
                if (!typeof(ArcanePlant).IsAssignableFrom(thingDef.thingClass)) { continue; }

                var extension = thingDef.GetModExtension<GrowingArcanePlantData>();
                if (extension != null && thingDef.IsResearchFinished)
                {
                    _plantCandidates.Add(thingDef);
                }
            }

        }

        public override void DoWindowContents(Rect inRect)
        {
            var baseRect = new RectDivider(inRect.ContractedBy(10f), 57348572, new Vector2(10f, 0f));

            var buttonAreaRect = baseRect.NewRow(40f, VerticalJustification.Bottom);
            {
                if (_currentSelected != null)
                {
                    var half = buttonAreaRect.NewCol(buttonAreaRect.Rect.width / 2f, HorizontalJustification.Left, marginOverride: 0f);
                    var acceptButtonRect = new Rect(0f, 0f, 120f, 40f);
                    acceptButtonRect.center = half.Rect.center;
                    acceptButtonRect.y += 10f;

                    if (Widgets.ButtonText(acceptButtonRect, "Accept".Translate()))
                    {
                        _callback(new GrowingArcanePlantBill(_billGiver, _currentSelected));
                        Close();
                    }
                }

                var closeButtonRect = new Rect(0f, 0f, 120f, 40f);
                closeButtonRect.center = buttonAreaRect.Rect.center;
                closeButtonRect.y += 10f;
                if (Widgets.ButtonText(closeButtonRect, CloseButtonText))
                {
                    Close();
                }
            }

            var ingredientAreaRect = baseRect.NewRow(40f, VerticalJustification.Bottom);
            {
                var lineRect = ingredientAreaRect.NewRow(4f);
                {
                    GUI.BeginGroup(lineRect);
                    GUI.color = Widgets.SeparatorLineColor;
                    Widgets.DrawLineHorizontal(0f, 0f, lineRect.Rect.width);
                    GUI.EndGroup();
                }

                var ingredientSectionRect = new RectDivider(ingredientAreaRect, 1244848, new Vector2(4f, 0f));
                if (_currentSelected != null)
                {
                    var growData = _currentSelected.GetModExtension<GrowingArcanePlantData>();
                    if (growData != null && growData.ingredients != null)
                    {
                        var ingredients = growData.ingredients.ToList();
                        foreach (var ingredient in ingredients)
                        {
                            var label = ingredient.LabelCap;

                            var width = 44f + Text.CalcSize(label).x;
                            var rect = ingredientSectionRect.NewCol(width);
                            if (Mouse.IsOver(rect))
                            {
                                Widgets.DrawHighlight(rect);
                                TooltipHandler.TipRegion(rect, ingredient.thingDef.DescriptionDetailed + "\n\n" + "ClickForMoreInfo".Translate().Colorize(ColoredText.SubtleGrayColor));
                            }

                            try
                            {
                                Text.Anchor = TextAnchor.MiddleLeft;
                                Widgets.DefIcon(rect.NewCol(rect.Rect.height), ingredient.thingDef);
                                Widgets.Label(rect, label);
                            }
                            finally
                            {
                                Text.Anchor = TextAnchor.UpperLeft;
                            }

                            if (Widgets.ButtonInvisible(rect))
                            {
                                Find.WindowStack.Add(new Dialog_InfoCard(ingredient.thingDef));
                            }
                        }
                    }
                }
            }

            var ingredientLabelRect = baseRect.NewRow(20f, VerticalJustification.Bottom);
            {
                try
                {
                    GUI.color = Widgets.SeparatorLabelColor;
                    var rect = ingredientLabelRect.Rect;
                    Text.Anchor = TextAnchor.MiddleLeft;
                    Widgets.Label(rect, LocalizeString_Dialog.VV_DialogStartGrowingArcanePlantIngredientLabel.Translate());
                }
                finally
                {
                    GUI.color = Color.white;
                    Text.Anchor = TextAnchor.UpperLeft;
                }
            }

            baseRect.NewRow(8f, VerticalJustification.Bottom);

            var leftAreaRect = new RectDivider(baseRect.NewCol(Mathf.Max(4f, InitialSize.x - OptionWidth - 10f), HorizontalJustification.Left), 874987234, new Vector2(10f, 5f));
            {
                try
                {
                    var titleRect = leftAreaRect.NewRow(40f);
                    try
                    {
                        Text.Font = GameFont.Medium;
                        Text.Anchor = TextAnchor.MiddleLeft;
                        if (_currentSelected == null)
                        {
                            Widgets.Label(titleRect, LocalizeTexts.DialogGrowArcanePlantTitleChooseOption.Translate());
                        }
                        else
                        {
                            var thingIconSectionRect = titleRect.NewCol(40f, HorizontalJustification.Left);
                            var thingIconRect = new Rect(0f, 0f, 40f, 40f);
                            thingIconRect.center = thingIconSectionRect.Rect.center;
                            Widgets.DefIcon(thingIconRect, _currentSelected);
    
                            var infoIconSectionRect = titleRect.NewCol(30f, HorizontalJustification.Right);
                            var infoIconRect = new Rect(0f, 0f, 24f, 24f);
                            infoIconRect.center = infoIconSectionRect.Rect.center;
                            Widgets.InfoCardButton(infoIconRect, _currentSelected);

                            Widgets.Label(titleRect, LocalizeTexts.DialogGrowArcanePlantTitleSelected.Translate(_currentSelected.LabelCap));
                        }
                    }
                    finally
                    {
                        Text.Anchor = TextAnchor.UpperLeft;
                        Text.Font = GameFont.Small;
                    }

                    if (_currentSelected != null)
                    {
                        var descRect = new RectDivider(leftAreaRect, 86837113, new Vector2(0f, 2f));
                        var growData = _currentSelected.GetModExtension<GrowingArcanePlantData>();
                        if (growData != null)
                        {
                            var width = descRect.Rect.width;
                            var description = _currentSelected.description;
                            LabelText(ref descRect, description);

                            DrawLine(ref descRect);

                            LabelText(
                                ref descRect,
                                LocalizeTexts.DialogGrowArcanePlantTotalGrowDays.Translate(growData.totalGrowDays.ToString("0.#")));

                            LabelText(
                                ref descRect,
                                LocalizeTexts.DialogGrowArcanePlantExpectedAmount.Translate(growData.baseAmount),
                                LocalizeTexts.DialogGrowArcanePlantExpectedAmountDesc.Translate());

                            LabelText(
                                ref descRect,
                                LocalizeTexts.DialogGrowArcanePlantTotalHealth.Translate(growData.maxHealth),
                                LocalizeTexts.DialogGrowArcanePlantTotalHealthDesc.Translate());

                            LabelText(
                                ref descRect,
                                LocalizeTexts.DialogGrowArcanePlantTotalMana.Translate(growData.maxMana, growData.consumedManaByDay.ToString("0.#")),
                                LocalizeTexts.DialogGrowArcanePlantTotalManaDesc.Translate());

                            if (growData.ManaSensitivity > GrowingArcanePlantSensitivity.None)
                            {
                                LabelText(
                                    ref descRect,
                                    LocalizeTexts.DialogGrowArcanePlantManaSensitivity.Translate($"VV_PlantSensitivity_{growData.ManaSensitivity}".Translate()),
                                    LocalizeTexts.DialogGrowArcanePlantManaSensitivityDesc.Translate());
                            }

                            if (growData.ManageSensitivity > GrowingArcanePlantSensitivity.None)
                            {
                                DrawLine(ref descRect);

                                LabelText(
                                    ref descRect,
                                    LocalizeTexts.DialogGrowArcanePlantManageInterval.Translate(growData.manageIntervalTicks.ToStringTicksToPeriod()),
                                    LocalizeTexts.DialogGrowArcanePlantManageIntervalDesc.Translate());

                                LabelText(
                                    ref descRect,
                                    LocalizeTexts.DialogGrowArcanePlantManageSensitivity.Translate($"VV_PlantSensitivity_{growData.ManageSensitivity}".Translate()),
                                    LocalizeTexts.DialogGrowArcanePlantManageSensitivityDesc.Translate());
                            }


                            if (growData.TemperatureSensitivity > GrowingArcanePlantSensitivity.None)
                            {
                                DrawLine(ref descRect);

                                LabelText(
                                    ref descRect,
                                    LocalizeTexts.DialogGrowArcanePlantOptimalTemperature.Translate($"{growData.optimalTemperatureRange.TrueMin.ToStringTemperature()} ~ {growData.optimalTemperatureRange.TrueMax.ToStringTemperature()}"));

                                LabelText(
                                    ref descRect,
                                    LocalizeTexts.DialogGrowArcanePlantTemperatureSensitivity.Translate($"VV_PlantSensitivity_{growData.TemperatureSensitivity}".Translate()),
                                    LocalizeTexts.DialogGrowArcanePlantTemperatureSensitivityDesc.Translate());
                            }

                            if (growData.GlowSensitivity > GrowingArcanePlantSensitivity.None)
                            {
                                DrawLine(ref descRect);

                                LabelText(
                                    ref descRect,
                                    LocalizeTexts.DialogGrowArcanePlantOptimalGlow.Translate($"{growData.optimalGlowRange.TrueMin.ToStringPercent()} ~ {growData.optimalGlowRange.TrueMax.ToStringPercent()}"));

                                LabelText(
                                    ref descRect,
                                    LocalizeTexts.DialogGrowArcanePlantGlowSensitivity.Translate($"VV_PlantSensitivity_{growData.GlowSensitivity}".Translate()),
                                    LocalizeTexts.DialogGrowArcanePlantGlowSensitivityDesc.Translate());
                            }

                            void DrawLine(ref RectDivider divider)
                            {
                                Color color = GUI.color;
                                try
                                {
                                    var lineRect = divider.NewRow(4f);
                                    GUI.BeginGroup(lineRect);
                                    GUI.color = Widgets.SeparatorLineColor;
                                    Widgets.DrawLineHorizontal(0f, 0f, lineRect.Rect.width);
                                    GUI.EndGroup();
                                }
                                finally
                                {
                                    GUI.color = color;
                                }
                            }

                            void LabelText(ref RectDivider divider, string text, string tooltip = null)
                            {
                                var height = Text.CalcHeight(text, width);
                                var rect = divider.NewRow(height);

                                if (tooltip != null && Mouse.IsOver(rect))
                                {
                                    Widgets.DrawHighlight(rect);
                                }

                                Widgets.Label(rect, text);

                                if (tooltip != null)
                                {
                                    TooltipHandler.TipRegion(rect, tooltip);
                                }
                            }
                        }
                    }
                }
                finally
                {
                    Text.Font = GameFont.Small;
                    Text.Anchor = TextAnchor.UpperLeft;
                }
            }

            var rightAreaRect = baseRect;
            {
                const float scrollMargin = 10f;
                Widgets.DrawMenuSection(rightAreaRect);

                var scrollOutRect = rightAreaRect.Rect.ContractedBy(10f);
                var scrollViewRect = new Rect(
                    0f, 
                    0f, 
                    Mathf.Max(scrollOutRect.width - 21f, 18f), 
                    Mathf.Max(scrollOutRect.height, _plantCandidates.Count * (OptionHeight + scrollMargin) - scrollMargin));

                Widgets.BeginScrollView(scrollOutRect, ref _scrollPosition, scrollViewRect);
                Widgets.BeginGroup(scrollViewRect);

                var inScrollViewRectDivider = new RectDivider(scrollViewRect, 47348521, new Vector2(0f, scrollMargin));

                for (int i = 0; i < _plantCandidates.Count; ++i)
                {
                    var optionRect = inScrollViewRectDivider.NewRow(OptionHeight);
                    DrawArcanePlantElement(optionRect, _plantCandidates[i]);
                }

                Widgets.EndGroup();
                Widgets.EndScrollView();
            }
        }

        private void DrawArcanePlantElement(Rect optionRect, ThingDef thingDef)
        {
            if (thingDef == _currentSelected)
            {
                Widgets.DrawBoxSolidWithOutline(optionRect, TexUI.HighlightBgResearchColor, TexUI.HighlightBorderResearchColor);
            }
            else
            {
                Widgets.DrawBoxSolidWithOutline(optionRect, TexUI.AvailResearchColor, TexUI.DefaultBorderResearchColor);
            }

            var divider = new RectDivider(optionRect.ContractedBy(4f), 743589672, new Vector2(10f, 0f));
            var iconRect = divider.NewCol(optionRect.height);
            Widgets.DefIcon(iconRect, thingDef);

            try
            {
                GUI.color = Color.white;
                Text.Anchor = TextAnchor.MiddleLeft;

                Widgets.Label(divider.Rect, thingDef.LabelCap);
            }
            finally
            {
                GUI.color = Color.white;
                Text.Anchor = TextAnchor.UpperLeft;
            }

            if (Widgets.ButtonInvisible(optionRect))
            {
                _currentSelected = thingDef;
                SoundDefOf.Click.PlayOneShotOnCamera();
            }
        }
    }
}
