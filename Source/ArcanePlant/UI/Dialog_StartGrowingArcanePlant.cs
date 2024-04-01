using LudeonTK;
using RimWorld;
using System;
using System.Collections.Generic;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace VVRace
{
    public class Dialog_StartGrowingArcanePlant : Window
    {
        public override Vector2 InitialSize => new Vector2(Mathf.Min(650f, UI.screenWidth), 700f);

        private Building_ArcanePlantFarm _billGiver;
        private Action<GrowArcanePlantBill> _callback;

        private List<ThingDef> _plantCandidates;
        private Vector2 _scrollPosition;

        private ThingDef _currentSelected;

        private const float OptionWidth = 300f;
        private const float OptionHeight = 50f;

        public Dialog_StartGrowingArcanePlant(
            Building_ArcanePlantFarm billGiver, 
            Action<GrowArcanePlantBill> callback)
        {
            forcePause = true;
            closeOnAccept = false;
            doCloseX = true;
            doCloseButton = false;

            _billGiver = billGiver;
            _callback = callback;

            _plantCandidates = new List<ThingDef>();
            foreach (var thingDef in DefDatabase<ThingDef>.AllDefsListForReading)
            {
                if (!typeof(ArcanePlant).IsAssignableFrom(thingDef.thingClass)) { continue; }

                var extension = thingDef.GetModExtension<GrowArcanePlantData>();
                if (extension != null && thingDef.IsResearchFinished)
                {
                    _plantCandidates.Add(thingDef);
                }
            }

        }

        public override void DoWindowContents(Rect inRect)
        {
            var baseRect = new RectDivider(inRect.ContractedBy(10f), 57348572, new Vector2(10f, 0f));
            var bottomAreaRect = baseRect.NewRow(60f, VerticalJustification.Bottom);
            {
                if (_currentSelected != null)
                {
                    var half = bottomAreaRect.NewCol(bottomAreaRect.Rect.width / 2f, HorizontalJustification.Left, marginOverride: 0f);
                    var acceptButtonRect = new Rect(0f, 0f, 120f, 40f);
                    acceptButtonRect.center = half.Rect.center;
                    acceptButtonRect.y += 10f;

                    if (Widgets.ButtonText(acceptButtonRect, "Accept".Translate()))
                    {
                        _callback(new GrowArcanePlantBill(_billGiver, _currentSelected));
                        Close();
                    }
                }

                var closeButtonRect = new Rect(0f, 0f, 120f, 40f);
                closeButtonRect.center = bottomAreaRect.Rect.center;
                closeButtonRect.y += 10f;
                if (Widgets.ButtonText(closeButtonRect, CloseButtonText))
                {
                    Close();
                }
            }

            var leftAreaRect = baseRect.NewCol(Mathf.Max(4f, InitialSize.x - OptionWidth - 10f), HorizontalJustification.Left);
            {
                try
                {
                    Text.Font = GameFont.Medium;
                    Text.Anchor = TextAnchor.UpperLeft;
                    var titleRect = leftAreaRect.NewRow(40f);
                    if (_currentSelected == null)
                    {
                        Widgets.Label(titleRect, LocalizeTexts.DialogGrowArcanePlantTitleChooseOption.Translate());
                    }
                    else
                    {
                        var iconSectionRect = titleRect.NewCol(40f, HorizontalJustification.Right);
                        var iconRect = new Rect(0f, 0f, 24f, 24f);
                        iconRect.center = iconSectionRect.Rect.center;
                        Widgets.InfoCardButton(iconRect, _currentSelected);
                        Widgets.Label(titleRect, LocalizeTexts.DialogGrowArcanePlantTitleSelected.Translate(_currentSelected.LabelCap));
                    }

                    Text.Font = GameFont.Small;
                    if (_currentSelected != null)
                    {
                        var descRect = new RectDivider(leftAreaRect, 86837113, new Vector2(0f, 4f));
                        var growData = _currentSelected.GetModExtension<GrowArcanePlantData>();
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
                                LocalizeTexts.DialogGrowArcanePlantTotalMana.Translate(growData.maxMana),
                                LocalizeTexts.DialogGrowArcanePlantTotalManaDesc.Translate());

                            if (growData.ManaSensitivity > GrowArcanePlantSensitivity.None)
                            {
                                LabelText(
                                    ref descRect,
                                    LocalizeTexts.DialogGrowArcanePlantManaSensitivity.Translate($"VV_PlantSensitivity_{growData.ManaSensitivity}".Translate()),
                                    LocalizeTexts.DialogGrowArcanePlantManaSensitivityDesc.Translate());
                            }

                            if (growData.ManageSensitivity > GrowArcanePlantSensitivity.None)
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


                            if (growData.TemperatureSensitivity > GrowArcanePlantSensitivity.None)
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

                            if (growData.GlowSensitivity > GrowArcanePlantSensitivity.None)
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
