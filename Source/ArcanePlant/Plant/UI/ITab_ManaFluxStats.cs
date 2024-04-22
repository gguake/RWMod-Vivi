using RimWorld;
using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace VVRace
{
    public class ITab_ManaFluxStats : ITab
    {
        public ManaFluxNetwork FluxNetwork => (SelThing as ManaAcceptor)?.ManaFluxNetwork;

        public ITab_ManaFluxStats()
        {
            size = new Vector2(800f, 450f);
            labelKey = LocalizeString_ITab.VV_ITab_ManaAcceptor_StatsTabLabel.Translate();
        }

        public override bool IsVisible => FluxNetwork != null && FluxNetwork.FluxHistory.Count > 1;

        private static List<SimpleCurveDrawInfo> _tmpDrawInfoList = new List<SimpleCurveDrawInfo>();
        protected override void FillTab()
        {
            var network = FluxNetwork;
            if (network == null) { return; }

            var mainRect = new RectDivider(new Rect(new Vector2(0f, 0f), size).ContractedBy(20f), 578689373);
            var legendRect = mainRect.NewRow(40f, VerticalJustification.Bottom);
            var graphRect = mainRect.Rect;

            _tmpDrawInfoList.Clear();
            var generatedCurve = new SimpleCurveDrawInfo();
            generatedCurve.color = Color.green;
            generatedCurve.label = LocalizeString_ITab.VV_ITab_ManaAcceptor_ManaGenCurveLabel.Translate();
            generatedCurve.valueFormat = "{0}";
            generatedCurve.curve = new SimpleCurve();
            _tmpDrawInfoList.Add(generatedCurve);

            var consumedCurve = new SimpleCurveDrawInfo();
            consumedCurve.color = Color.red;
            consumedCurve.label = LocalizeString_ITab.VV_ITab_ManaAcceptor_ManaConCurveLabel.Translate();
            consumedCurve.valueFormat = "{0}";
            consumedCurve.curve = new SimpleCurve();
            _tmpDrawInfoList.Add(consumedCurve);

            var transferedCurve = new SimpleCurveDrawInfo();
            transferedCurve.color = Color.yellow;
            transferedCurve.label = LocalizeString_ITab.VV_ITab_ManaAcceptor_ManaTrsCurveLabel.Translate();
            transferedCurve.valueFormat = "{0}";
            transferedCurve.curve = new SimpleCurve();
            _tmpDrawInfoList.Add(transferedCurve);

            var exceededCurve = new SimpleCurveDrawInfo();
            exceededCurve.color = Color.blue;
            exceededCurve.label = LocalizeString_ITab.VV_ITab_ManaAcceptor_ManaExcCurveLabel.Translate();
            exceededCurve.valueFormat = "{0}";
            exceededCurve.curve = new SimpleCurve();
            _tmpDrawInfoList.Add(exceededCurve);

            int minTick = int.MaxValue, maxTick = 0;
            foreach (var history in network.FluxHistory)
            {
                if (history.tick < minTick) { minTick = history.tick; }
                if (history.tick > maxTick) { maxTick = history.tick; }

                generatedCurve.curve.Add(new CurvePoint(history.tick / 60000f, (int)(history.generated * GenTicks.TickRareInterval)), sort: false);
                consumedCurve.curve.Add(new CurvePoint(history.tick / 60000f, (int)(history.consumed * GenTicks.TickRareInterval)), sort: false);
                transferedCurve.curve.Add(new CurvePoint(history.tick / 60000f, (int)(history.transfered * GenTicks.TickRareInterval)), sort: false);
                exceededCurve.curve.Add(new CurvePoint(history.tick / 60000f, (int)(history.exceeded * GenTicks.TickRareInterval)), sort: false);
            }

            generatedCurve.curve.SortPoints();
            consumedCurve.curve.SortPoints();
            transferedCurve.curve.SortPoints();
            exceededCurve.curve.SortPoints();
            if (network.FluxHistory.Count == 1)
            {
                generatedCurve.curve.Add(new CurvePoint(1.6666667E-05f, (int)(network.FluxHistory.Peek().generated * GenTicks.TickRareInterval)));
                consumedCurve.curve.Add(new CurvePoint(1.6666667E-05f, (int)(network.FluxHistory.Peek().consumed * GenTicks.TickRareInterval)));
                transferedCurve.curve.Add(new CurvePoint(1.6666667E-05f, (int)(network.FluxHistory.Peek().transfered * GenTicks.TickRareInterval)));
                exceededCurve.curve.Add(new CurvePoint(1.6666667E-05f, (int)(network.FluxHistory.Peek().exceeded * GenTicks.TickRareInterval)));
            }

            var section = new FloatRange(minTick / 60000f, maxTick / 60000f);
            if (Mathf.Approximately(section.min, section.max))
            {
                section.max += 1.6666667E-05f;
            }

            var curveDrawerStyle = Find.History.curveDrawerStyle;
            curveDrawerStyle.FixedSection = section;
            curveDrawerStyle.YIntegersOnly = true;

            SimpleCurveDrawer.DrawCurves(graphRect, _tmpDrawInfoList, curveDrawerStyle, legendRect: legendRect);

            Text.Anchor = TextAnchor.UpperLeft;
            TooltipHandler.TipRegion(legendRect, LocalizeString_ITab.VV_ITab_ManaAcceptor_LegendTooltip.Translate());
        }
    }
}
