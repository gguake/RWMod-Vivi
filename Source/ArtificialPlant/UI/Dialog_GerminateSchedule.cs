using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace VVRace
{
    public class Dialog_GerminateSchedule : Window
    {
        public override Vector2 InitialSize => new Vector2(546f, 582f);

        private List<Building_SeedlingGerminator> _germinators;
        private GerminateSchedule _schedule;
        private GerminateScheduleDef _selectedGerminateScheduleDef = VVGerminateScheduleDefOf.VV_DoNothing;

        public Dialog_GerminateSchedule(IEnumerable<Building_SeedlingGerminator> germinators, ThingDef fixedGerminateResult)
        {
            _germinators = germinators.ToList();
            _schedule = new GerminateSchedule(_germinators[0].def, fixedGerminateResult);

            var lastScheduleBuilding = _germinators.Where(v => v.LastProcessedSchedule != null).FirstOrDefault();
            if (lastScheduleBuilding != null)
            {
                for (int i = 0; i < GerminateSchedule.TotalScheduleCount; ++i)
                {
                    _schedule[i] = lastScheduleBuilding.LastProcessedSchedule[i];
                }
            }

            forcePause = true;
            doCloseX = true;
            absorbInputAroundWindow = true;
            closeOnAccept = false;
            closeOnClickedOutside = false;
        }

        public override void DoWindowContents(Rect inRect)
        {
            Text.Font = GameFont.Small;
            var divider = new RectDivider(inRect, 1138092823);
            var dividerHeader = new RectDivider(divider.NewRow(60f), 3472381, new Vector2(2f, 0f));

            #region 헤더
            {
                var headerRect = dividerHeader.NewRow(44f);
                var iconRect = headerRect.NewCol(44f);
                Widgets.DefIcon(iconRect, _schedule.FixedGerminateResult ?? VVThingDefOf.VV_UnknownSeed, scale: 0.75f);
                try
                {
                    var headerText = _schedule.FixedGerminateResult == null ?
                        LocalizeTexts.FloatMenuOptionDefaultGerminate.Translate() :
                        LocalizeTexts.FloatMenuOptionFixedGerminate.Translate(_schedule.FixedGerminateResult.LabelCap);

                    Text.Anchor = TextAnchor.MiddleLeft;
                    Text.Font = GameFont.Medium;
                    Widgets.Label(headerRect, headerText);
                }
                finally
                {
                    Text.Anchor = TextAnchor.UpperLeft;
                    Text.Font = GameFont.Small;
                }


                var lineRect = dividerHeader.NewRow(1f).Rect;
                Widgets.DrawLine(
                    new Vector2(lineRect.xMin, lineRect.yMin),
                    new Vector2(lineRect.xMax, lineRect.yMin),
                    Widgets.SeparatorLineColor,
                    1f);
            }
            #endregion

            var dividerUpper = divider.NewRow(248f);
            var dividerLower = divider;

            var dividerSelectionScheduleDef = dividerUpper.NewCol(185f);
            #region 스케줄 선택 영역
            {
                var headerRect = dividerSelectionScheduleDef.NewRow(24f);
                Widgets.Label(headerRect, LocalizeTexts.ViviGerminateWindowJobTab.Translate());

                var scheduleDefs = DefDatabase<GerminateScheduleDef>.AllDefsListForReading.OrderBy(v => v.uiOrder);
                foreach (var scheduleDef in scheduleDefs)
                {
                    var rowRect = dividerSelectionScheduleDef.NewRow(40f);
                    DrawGerminateScheduleDefRow(rowRect, scheduleDef);
                }
            }
            #endregion

            var dividerScheduleInfo = dividerUpper;
            #region 스케줄 정보 영역
            {
                var scheduleCooldown = _germinators[0].GerminatorModExtension.scheduleCooldown;

                var headerRect = dividerScheduleInfo.NewRow(24f);
                Widgets.Label(headerRect, LocalizeTexts.ViviGerminateWindowScheduleTab.Translate());

                var scheduleRowRect = new RectDivider(dividerScheduleInfo.NewRow(76f), 43283471, new Vector2(4f, 4f));
                for (int i = 0; i < GerminateSchedule.TotalScheduleCount; ++i)
                {
                    var colRect = scheduleRowRect.NewCol(48);
                    DrawGernmiateScheduleColumn(colRect, _schedule, i, () =>
                    {
                        _schedule[i] = _selectedGerminateScheduleDef;
                        SoundDefOf.Designate_DragStandard_Changed_NoCam.PlayOneShotOnCamera();
                    });
                }
                var lineRect = dividerScheduleInfo.NewRow(1f).Rect;
                Widgets.DrawLine(
                    new Vector2(lineRect.xMin, lineRect.yMin),
                    new Vector2(lineRect.xMax, lineRect.yMin),
                    Widgets.SeparatorLineColor,
                    1f);

                Widgets.Label(dividerScheduleInfo, LocalizeTexts.ViviGerminateWindowScheduleDesc.Translate(GerminateSchedule.TotalGerminateDays, scheduleCooldown / 2500));
            }
            #endregion

            #region 요약 영역
            {
                var lineRect = dividerLower.NewRow(1f).Rect;

                Widgets.DrawLine(
                    new Vector2(lineRect.xMin, lineRect.yMin),
                    new Vector2(lineRect.xMax, lineRect.yMin),
                    Widgets.SeparatorLineColor,
                    1f);

                DrawGerminateScheduleSummary(dividerLower);
            }
            #endregion

            #region 버튼 영역
            {
                var buttonRect = dividerLower.NewRow(48f, VerticalJustification.Bottom);
                var lineRect = buttonRect.NewRow(4f).Rect;

                Widgets.DrawLine(
                    new Vector2(lineRect.xMin, lineRect.yMin),
                    new Vector2(lineRect.xMax, lineRect.yMin),
                    Widgets.SeparatorLineColor,
                    1f);

                var buttonRectLeft = buttonRect.NewCol(buttonRect.Rect.width / 2f).Rect;
                var buttonRectRight = buttonRect.Rect;

                bool okButton = false;
                if (Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.Return)
                {
                    okButton = true;
                    Event.current.Use();
                }

                try
                {
                    GUI.color = new Color(0.3f, 1f, 0.35f);
                    var buttonOkRect = new Rect(0f, 0f, 120f, 40f);
                    buttonOkRect.center = buttonRectRight.center;
                    if (Widgets.ButtonText(buttonOkRect, "OK".Translate()) || okButton)
                    {
                        ConfirmDialog();
                        Close();
                    }
                }
                finally
                {
                    GUI.color = Color.white;
                }

                var buttonCancelRect = new Rect(0f, 0f, 120f, 40f);
                buttonCancelRect.center = buttonRectLeft.center;
                if (Widgets.ButtonText(buttonCancelRect, "Cancel".Translate()))
                {
                    Close();
                }
            }
            #endregion

        }

        private void DrawGerminateScheduleDefRow(RectDivider rect, GerminateScheduleDef def)
        {
            Widgets.DrawBoxSolid(rect, def.uiColor);

            if (Mouse.IsOver(rect))
            {
                Widgets.DrawHighlight(rect);

                if (Widgets.ButtonInvisible(rect))
                {
                    _selectedGerminateScheduleDef = def;
                    SoundDefOf.Designate_DragStandard_Changed_NoCam.PlayOneShotOnCamera();
                }
            }
            
            if (_selectedGerminateScheduleDef == def)
            {
                Widgets.DrawBox(rect, 2);
            }

            TooltipHandler.TipRegionByKey(rect, def.description);

            var iconRect = rect.NewCol(48);
            Widgets.DrawTextureFitted(iconRect, def.uiIcon, 1f);

            try
            {
                GUI.color = def.uiTextColor;
                Text.Anchor = TextAnchor.MiddleLeft;

                Widgets.Label(rect, def.LabelCap);
            }
            finally
            {
                Text.Anchor = TextAnchor.UpperLeft;
                GUI.color = Color.white;
            }
        }

        public static void DrawGernmiateScheduleColumn(RectDivider rect, GerminateSchedule schedule, int index, Action action)
        {
            var fullRect = rect.Rect;

            var scheduleDef = schedule[index];
            var headerRect = rect.NewRow(24);
            try
            {
                Text.Anchor = TextAnchor.MiddleCenter;
                Widgets.Label(headerRect, Find.ActiveLanguageWorker.OrdinalNumber(index + 1));
            }
            finally
            {
                Text.Anchor = TextAnchor.UpperLeft;
            }

            var blockRect = rect.NewRow(48);
            Widgets.DrawBoxSolid(blockRect, scheduleDef.uiColor);
            Widgets.DrawTextureFitted(blockRect, scheduleDef.uiIcon, 1.0f);

            if (Mouse.IsOver(blockRect))
            {
                Widgets.DrawHighlight(blockRect);

                if (Widgets.ButtonInvisible(blockRect))
                {
                    action();
                }
            }

            TooltipHandler.TipRegionByKey(fullRect, LocalizeTexts.ViviGerminateWindowScheduleTabTip.Translate(index + 1, schedule[index].LabelCap, schedule[index].description));
        }

        private void DrawGerminateScheduleSummary(RectDivider rect)
        {
            float totalWorkAmount = 0f;
            var dictIngredients = new Dictionary<ThingDef, int>();

            var data = _germinators[0].GerminatorModExtension;
            if (_schedule.IsFixedGerminate)
            {
                foreach (var tdc in data.fixedGerminateIngredients)
                {
                    if (dictIngredients.TryGetValue(tdc.thingDef, out var count))
                    {
                        dictIngredients[tdc.thingDef] = count + tdc.count;
                    }
                    else
                    {
                        dictIngredients.Add(tdc.thingDef, tdc.count);
                    }
                }

                dictIngredients.Add(_schedule.FixedGerminateResult, 1);
            }
            else
            {
                foreach (var tdc in data.germinateIngredients)
                {
                    if (dictIngredients.TryGetValue(tdc.thingDef, out var count))
                    {
                        dictIngredients[tdc.thingDef] = count + tdc.count;
                    }
                    else
                    {
                        dictIngredients.Add(tdc.thingDef, tdc.count);
                    }
                }
            }

            foreach (var scheduleDef in _schedule)
            {
                if (scheduleDef.ingredients != null)
                {
                    foreach (var tdc in scheduleDef.ingredients)
                    {
                        if (dictIngredients.TryGetValue(tdc.thingDef, out var count))
                        {
                            dictIngredients[tdc.thingDef] = count + tdc.count;
                        }
                        else
                        {
                            dictIngredients.Add(tdc.thingDef, tdc.count);
                        }
                    }
                }

                totalWorkAmount += scheduleDef.workAmount;
            }

            var rectIngredientArea = new RectDivider(rect.NewCol(180f), 3459831, new Vector2(4f, 4f));
            Widgets.Label(rectIngredientArea.NewRow(24f), LocalizeTexts.ViviGerminateWindowIngredientHeader.Translate());

            foreach (var kv in dictIngredients)
            {
                var labelText = string.Format("{0} x{1}", kv.Key.LabelCap, kv.Value);
                var rectThing = rectIngredientArea.NewRow(24);
                DrawIngredientLabel(rectThing, kv.Key, labelText);
            }

            var rectBonusSummaryArea = new RectDivider(rect, 13468279, new Vector2(4f, 4f));
            Widgets.Label(rectBonusSummaryArea.NewRow(26), LocalizeTexts.ViviGerminateWindowTotalWork.Translate(totalWorkAmount));

            var bonusCount = _schedule.ExpectedGerminateBonusCount;
            var bonusSuccessChance = _schedule.ExpectedGerminateBonusSuccessChance;
            var bonusRareChance = _schedule.ExpectedGerminateBonusRareChance;
            var bonusMutateChance = _schedule.ExpectedFixedGerminateMutateAnotherArtificialPlantChance;

            if (bonusCount != 0 || bonusSuccessChance != 1f || bonusRareChance != 1f)
            {
                Widgets.Label(rectBonusSummaryArea.NewRow(24f), LocalizeTexts.ViviGerminateWindowBonusSummaryHeader.Translate());
                if (bonusCount != 0)
                {
                    var rectLabel = new RectDivider(rectBonusSummaryArea.NewRow(24f), 78484321, new Vector2(2f, 0f));
                    var rectLabelIcon = rectLabel.NewCol(24);
                    Widgets.DrawTextureFitted(rectLabelIcon, Widgets.PlaceholderIconTex, 0.75f);
                    Widgets.Label(rectLabel, LocalizeTexts.ViviGerminateWindowBonusCount.Translate(bonusCount.ToString("+0.0;-0.0")));
                }

                if (bonusSuccessChance != 1f)
                {
                    var rectLabel = new RectDivider(rectBonusSummaryArea.NewRow(24f), 78484322, new Vector2(2f, 0f));
                    var rectLabelIcon = rectLabel.NewCol(24);
                    Widgets.DrawTextureFitted(rectLabelIcon, Widgets.PlaceholderIconTex, 0.75f);
                    Widgets.Label(rectLabel, LocalizeTexts.ViviGerminateWindowBonusSuccessChance.Translate(bonusSuccessChance.ToStringPercentEmptyZero()));
                }

                if (bonusRareChance != 1f && !_schedule.IsFixedGerminate)
                {
                    var rectLabel = new RectDivider(rectBonusSummaryArea.NewRow(24f), 78484323, new Vector2(2f, 0f));
                    var rectLabelIcon = rectLabel.NewCol(24);
                    Widgets.DrawTextureFitted(rectLabelIcon, Widgets.PlaceholderIconTex, 0.75f);
                    Widgets.Label(rectLabel, LocalizeTexts.ViviGerminateWindowBonusRareChance.Translate(bonusRareChance.ToStringPercentEmptyZero()));
                }

                if (bonusMutateChance != 0f && _schedule.IsFixedGerminate)
                {
                    var rectLabel = new RectDivider(rectBonusSummaryArea.NewRow(24f), 78484323, new Vector2(2f, 0f));
                    var rectLabelIcon = rectLabel.NewCol(24);
                    Widgets.DrawTextureFitted(rectLabelIcon, Widgets.PlaceholderIconTex, 0.75f);
                    Widgets.Label(rectLabel, LocalizeTexts.ViviGerminateWindowBonusMutateAnotherArtificialPlantChance.Translate(bonusMutateChance.ToStringPercentEmptyZero()));
                }
            }
        }

        private void DrawIngredientLabel(Rect rect, Def def, string label, float iconMargin = 2f, float textOffsetX = 6f)
        {
            Widgets.DrawHighlightIfMouseover(rect);
            TooltipHandler.TipRegion(rect, def.description);

            Widgets.BeginGroup(rect);
            try
            {
                Rect iconRect = new Rect(0f, 0f, rect.height, rect.height);
                if (iconMargin != 0f)
                {
                    iconRect = iconRect.ContractedBy(iconMargin);
                }

                Widgets.DefIcon(iconRect, def, null, 1f, null, drawPlaceholder: true);

                Rect rect3 = new Rect(iconRect.xMax + textOffsetX, 0f, rect.width, rect.height);
                Text.Anchor = TextAnchor.MiddleLeft;
                Text.WordWrap = false;

                Widgets.Label(rect3, label);
            }
            finally
            {
                Text.Anchor = TextAnchor.UpperLeft;
                Text.WordWrap = true;
                Widgets.EndGroup();
            }
        }

        private void ConfirmDialog()
        {
            foreach (var germinator in _germinators)
            {
                germinator.ReserveSchedule(_schedule.Clone());
            }

        }
    }
}
