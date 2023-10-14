using RimWorld;
using UnityEngine;
using Verse;

namespace VVRace
{
    public class ITab_GerminateSchedule : ITab
    {
        private static readonly Vector2 Size = new Vector2(328f, 130f);

        protected Building_SeedlingGerminator SelBuilding => (Building_SeedlingGerminator)base.SelThing;

        public ITab_GerminateSchedule()
        {
            size = Size;
            labelKey = LocalizeTexts.ITab_SeedlingGerminator_TabLabel.Translate();
        }

        protected override void FillTab()
        {
            var divider = new RectDivider(new Rect(0, 0, size.x, size.y).ContractedBy(10f), 477891134);

            var germinateSchedule = SelBuilding.CurrentSchedule;
            if (germinateSchedule == null || germinateSchedule.Stage == GerminateStage.None)
            {
                Text.Anchor = TextAnchor.MiddleCenter;
                Widgets.Label(divider.Rect, LocalizeTexts.ITab_SeedlingGerminator_MessageNoSchedule.Translate());
                Text.Anchor = TextAnchor.UpperLeft;
            }
            else
            {
                var labelRect = divider.NewRow(20f);
                Widgets.Label(labelRect, LocalizeTexts.ITab_SeedlingGerminator_CurrentScheduleLabel.Translate());

                var scheduleRowRect = new RectDivider(divider.NewRow(84f), 43283471, new Vector2(4f, 4f));
                for (int i = 0; i < GerminateSchedule.TotalScheduleCount; ++i)
                {
                    var colRect = scheduleRowRect.NewCol(48);
                    var colInnerRect = new RectDivider(colRect, 73856190, new Vector2(0f, 0f));
                    Dialog_GerminateSchedule.DrawGernmiateScheduleColumn(colInnerRect.NewRow(72), germinateSchedule, i, () => { });
                    colInnerRect.NewRow(4f);

                    var scheduleStatusQualityColor = new Color(0.23f, 0.23f, 0.23f);
                    var scheduleStatusRect = colInnerRect.NewRow(6f);
                    var quality = germinateSchedule.GetGerminateQuality(i);
                    if (quality > 0.5f)
                    {
                        scheduleStatusQualityColor = Color.Lerp(new Color(1f, 1f, 0f), new Color(0f, 1f, 0f), Mathf.Clamp01((quality - 0.5f) * 2f));
                    }
                    else if (quality >= 0f)
                    {
                        scheduleStatusQualityColor = Color.Lerp(new Color(1f, 0f, 0f), new Color(1f, 1f, 0f), Mathf.Clamp01((quality + 0.5f) * 2f));
                    }

                    Widgets.DrawBoxSolid(scheduleStatusRect, scheduleStatusQualityColor);
                }
            }
        }
    }
}
