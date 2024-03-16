using RimWorld;
using UnityEngine;
using Verse;

namespace VVRace
{
    public class ITab_GatherWorkTable : ITab
    {
        private static readonly Vector2 Size = new Vector2(420f, 64f);

        protected Building_GatherWorkTable SelTable => (Building_GatherWorkTable)base.SelThing;

        public ITab_GatherWorkTable()
        {
            size = Size;
            labelKey = LocalizeTexts.ITab_GatherWorkerTable_TabLabel.Translate();
        }

        protected override void FillTab()
        {
            var divider = new RectDivider(new Rect(0, 0, size.x, size.y).ContractedBy(10f), 477891134);
            var labelRect = divider.NewRow(20f);

            Text.Font = GameFont.Tiny;
            Widgets.Label(labelRect, LocalizeTexts.ITab_GatherWorkerTable_GatherRadiusSlider.Translate());

            var sliderRect = divider.NewRow(20f);
            var value = Widgets.HorizontalSlider(
                sliderRect,
                (int)SelTable.GatherRadius,
                (int)Building_GatherWorkTable.GatherMinRadius,
                (int)Building_GatherWorkTable.GatherMaxRadius,
                roundTo: 1f) + 0.9f;

            SelTable.GatherRadius = value;
        }
    }
}
