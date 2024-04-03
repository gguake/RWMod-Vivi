using LudeonTK;
using RimWorld;
using System.Security.Cryptography;
using UnityEngine;
using Verse;

namespace VVRace
{
    public class ITab_ArcanePlantFarmBill : ITab
    {
        public GrowingArcanePlantBill Bill => (SelThing as Building_ArcanePlantFarm)?.Bill;

        public override bool IsVisible => Bill != null && Bill.Stage >= GrowingArcanePlantBillStage.Growing;

        public ITab_ArcanePlantFarmBill()
        {
            size = new Vector2(500f, TabHeight);
            labelKey = LocalizeString_ITab.ITab_ArcanePlantFarmBill_TabLabel;
        }

        public static float TabHeight = 300f;

        protected override void FillTab()
        {
            var bill = Bill;
            if (bill == null) { return; }

            var mainRectDivider = new RectDivider(new Rect(new Vector2(0f, 0f), size).ContractedBy(20f), 9587572);

            var labelRect = new RectDivider(mainRectDivider.NewRow(40f), 2451196, new Vector2(10f, 0f));
            {
                var iconRect = labelRect.NewCol(40f);
                Widgets.DefIcon(iconRect, bill.RecipeTarget);

                try
                {
                    Text.Font = GameFont.Medium;
                    Text.Anchor = TextAnchor.MiddleLeft;
                    Widgets.Label(labelRect, LocalizeString_ITab.ITab_ArcanePlantFarmBill_Title.Translate(bill.RecipeTarget.LabelCap));
                }
                finally
                {
                    Text.Font = GameFont.Small;
                    Text.Anchor = TextAnchor.UpperLeft;
                }
            }

            var growthRect = mainRectDivider.NewRow(40f);
            {
                if (Mouse.IsOver(growthRect))
                {
                    Widgets.DrawHighlight(growthRect);
                }

                var label = LocalizeString_ITab.ITab_ArcanePlantFarmBill_GrowthPct.Translate();
                var boxRect = growthRect.Rect.ContractedBy(5f);
                try
                {
                    Text.Anchor = TextAnchor.MiddleLeft;
                    Widgets.FillableBarLabeled(boxRect, bill.TotalGrowthPct, Mathf.Max((int)Text.CalcSize(label).x + 5, 90), label);
                }
                finally
                {
                    Text.Anchor = TextAnchor.UpperLeft;
                }

                var desc = LocalizeString_ITab.ITab_ArcanePlantFarmBill_GrowthPctDesc.Translate();
                TooltipHandler.TipRegion(growthRect, $"{label}: {bill.TotalGrowthPct.ToStringPercent()}".Colorize(Color.yellow) + $"\n{desc}");
            }

            var dividedWidth = mainRectDivider.Rect.width / 2f - 5f;
            var leftRect = mainRectDivider.NewCol(dividedWidth, HorizontalJustification.Left, 0f);
            var rightRect = mainRectDivider.NewCol(dividedWidth, HorizontalJustification.Right, 0f);

            int index = 0;
            DrawNeedRect(
                ref index, 
                GrowingArcanePlantSensitivity.None, 
                LocalizeString_ITab.ITab_ArcanePlantFarmBill_HealthPct.Translate(), 
                LocalizeString_ITab.ITab_ArcanePlantFarmBill_HealthPctDesc.Translate(),
                bill.HealthPct, 
                bill.LastHealthChangeOffset / bill.Data.maxHealth);

            DrawNeedRect(
                ref index, 
                bill.Data.manaSensitivity, 
                LocalizeString_ITab.ITab_ArcanePlantFarmBill_ManaPct.Translate(), 
                LocalizeString_ITab.ITab_ArcanePlantFarmBill_ManaPctDesc.Translate(),
                bill.ManaPct);

            if (bill.Data.manageSensitivity != GrowingArcanePlantSensitivity.None)
            {
                DrawNeedRect(
                    ref index, 
                    bill.Data.manageSensitivity, 
                    LocalizeString_ITab.ITab_ArcanePlantFarmBill_Management.Translate(), 
                    LocalizeString_ITab.ITab_ArcanePlantFarmBill_ManagementDesc.Translate(bill.Data.manageIntervalTicks.ToStringTicksToPeriod()),
                    bill.ManagePct);
            }

            if (bill.Data.temperatureSensitivity != GrowingArcanePlantSensitivity.None)
            {
                float? temperatureOffset = null;
                if (!bill.IsGoodTemperature) { temperatureOffset = -0.1f; }
                else if (bill.TemperatureSeverity < 1f) { temperatureOffset = 0.1f; }
                
                DrawNeedRect(
                    ref index, 
                    bill.Data.temperatureSensitivity, 
                    LocalizeString_ITab.ITab_ArcanePlantFarmBill_Temperature.Translate(), 
                    LocalizeString_ITab.ITab_ArcanePlantFarmBill_TemperatureDesc.Translate(
                        bill.Data.optimalTemperatureRange.TrueMin, 
                        bill.Data.optimalTemperatureRange.TrueMax),
                    bill.TemperatureSeverity,
                    temperatureOffset);
            }

            if (bill.Data.glowSensitivity != GrowingArcanePlantSensitivity.None)
            {
                float? glowOffset = null;
                if (!bill.IsGoodGlow) { glowOffset = -0.1f; }
                else if (bill.GlowSeverity < 1f) { glowOffset = 0.1f; }

                DrawNeedRect(
                    ref index, 
                    bill.Data.glowSensitivity, 
                    LocalizeString_ITab.ITab_ArcanePlantFarmBill_Glow.Translate(), 
                    LocalizeString_ITab.ITab_ArcanePlantFarmBill_GlowDesc.Translate(
                        bill.Data.optimalGlowRange.TrueMin.ToStringPercent(), 
                        bill.Data.optimalGlowRange.TrueMax.ToStringPercent()),
                    bill.GlowSeverity,
                    glowOffset);
            }

            void DrawNeedRect(ref int idx, GrowingArcanePlantSensitivity sensitivity, TaggedString label, TaggedString desc, float pct, float? offset = null)
            {
                var rect = new RectDivider(index % 2 == 0 ? leftRect.NewRow(54f) : rightRect.NewRow(54f), 86075968, new Vector2(0f, 0f));
                var fullRect = rect.Rect;
                if (Mouse.IsOver(fullRect))
                {
                    Widgets.DrawHighlight(fullRect);
                }
                
                try
                {
                    Text.Anchor = TextAnchor.MiddleLeft;
                    var needLabelRect = rect.NewRow(20f).Rect;
                    needLabelRect.x += 5f;
                    Widgets.Label(needLabelRect, label);


                    var barAreaRect = new Rect(0f, 0f, rect.Rect.width - 40f, rect.Rect.height - 10f);
                    barAreaRect.center = rect.Rect.center;
                    var barRect = Widgets.FillableBar(barAreaRect, pct);
                    if (sensitivity > GrowingArcanePlantSensitivity.None)
                    {
                        var thresholds = sensitivity.CalcThreshold();
                        DrawBarThreshold(barRect, pct, thresholds.danger);
                        DrawBarThreshold(barRect, pct, thresholds.critical);
                    }

                    if (offset != null)
                    {
                        Widgets.FillableBarChangeArrows(barRect, offset.Value);
                    }
                }
                finally
                {
                    Text.Anchor = TextAnchor.UpperLeft;
                }

                TooltipHandler.TipRegion(fullRect, $"{label}: {pct.ToStringPercent()}".Colorize(Color.yellow) + $"\n{desc}");
                idx++;
            }
        }

        protected void DrawBarThreshold(Rect barRect, float curPct, float threshPct)
        {
            var num = ((!(barRect.width > 60f)) ? 1 : 2);
            var position = new Rect(barRect.x + barRect.width * threshPct - (num - 1f), barRect.y + barRect.height / 2f, num, barRect.height / 2f);
            Texture2D image;

            if (threshPct < curPct)
            {
                image = BaseContent.BlackTex;
                GUI.color = new Color(1f, 1f, 1f, 0.9f);
            }
            else
            {
                image = BaseContent.GreyTex;
                GUI.color = new Color(1f, 1f, 1f, 0.5f);
            }

            GUI.DrawTexture(position, image);
            GUI.color = Color.white;
        }
    }
}
