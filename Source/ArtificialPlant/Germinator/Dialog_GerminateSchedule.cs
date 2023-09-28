using RimWorld;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace VVRace
{
    public class Dialog_GerminateSchedule : Window
    {
        public override Vector2 InitialSize => new Vector2(546f, 488f);

        private List<Building_SeedlingGerminator> _germinators;
        private GerminateSchedule _schedule;
        private GerminateScheduleDef _selectedGerminateScheduleDef = VVGerminateScheduleDefOf.VV_DoNothing;

        public Dialog_GerminateSchedule(IEnumerable<Building_SeedlingGerminator> germinators)
        {
            _germinators = germinators.ToList();
            _schedule = new GerminateSchedule();

            forcePause = true;
            absorbInputAroundWindow = true;

        }

        public override void DoWindowContents(Rect inRect)
        {
            Text.Font = GameFont.Small;
            var divider = new RectDivider(inRect, 1138092823);
            var dividerUpper = divider.NewRow(244f);
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
                var headerRect = dividerScheduleInfo.NewRow(24f);
                Widgets.Label(headerRect, LocalizeTexts.ViviGerminateWindowScheduleTab.Translate());

                var scheduleRowRect = new RectDivider(dividerScheduleInfo.NewRow(76f), 43283471, new Vector2(4f, 4f));
                for (int i = 0; i < GerminateSchedule.ScheduleDayCount; ++i)
                {
                    var colRect = scheduleRowRect.NewCol(48);
                    DrawGernmiateScheduleColumn(colRect, i);
                }
                var lineRect = dividerScheduleInfo.NewRow(1f).Rect;
                Widgets.DrawLine(
                    new Vector2(lineRect.xMin, lineRect.yMin),
                    new Vector2(lineRect.xMax, lineRect.yMin),
                    Widgets.SeparatorLineColor,
                    1f);

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

                Widgets.DrawBoxSolid(dividerLower.Rect, Color.white);
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

        private void DrawGernmiateScheduleColumn(RectDivider rect, int index)
        {
            var fullRect = rect.Rect;

            var schedule = _schedule[index];
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
            Widgets.DrawBoxSolid(blockRect, schedule.uiColor);
            Widgets.DrawTextureFitted(blockRect, schedule.uiIcon, 1.0f);

            if (Mouse.IsOver(blockRect))
            {
                Widgets.DrawHighlight(blockRect);

                if (Widgets.ButtonInvisible(blockRect))
                {
                    _schedule[index] = _selectedGerminateScheduleDef;
                    SoundDefOf.Designate_DragStandard_Changed_NoCam.PlayOneShotOnCamera();
                }
            }

            TooltipHandler.TipRegionByKey(fullRect, LocalizeTexts.ViviGerminateWindowScheduleTabTip.Translate(index + 1, _schedule[index].LabelCap, _schedule[index].description));
        }
    }
}
