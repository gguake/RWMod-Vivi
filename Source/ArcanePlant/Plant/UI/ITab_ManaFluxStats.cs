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
            size = new Vector2(500f, 500f);
            labelKey = LocalizeString_ITab.VV_ITab_ManaAcceptor_StatsTabLabel.Translate();
        }

        public override bool IsVisible => FluxNetwork != null && FluxNetwork.FluxHistory.Count > 0;

        private static List<SimpleCurveDrawInfo> _tmpDrawInfoList;
        protected override void FillTab()
        {
            var network = FluxNetwork;
            if (network == null) { return; }

            var mainRect = new RectDivider(new Rect(new Vector2(0f, 0f), size).ContractedBy(20f), 578689373);
            var legendRect = mainRect.NewRow(40f, VerticalJustification.Bottom);
            var graphRect = mainRect.Rect;

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

                generatedCurve.curve.Add(new CurvePoint(history.tick / 60000f, history.generated), sort: false);
                consumedCurve.curve.Add(new CurvePoint(history.tick / 60000f, history.consumed), sort: false);
                exceededCurve.curve.Add(new CurvePoint(history.tick / 60000f, history.exceeded), sort: false);
            }

            generatedCurve.curve.SortPoints();
            consumedCurve.curve.SortPoints();
            exceededCurve.curve.SortPoints();
            if (network.FluxHistory.Count == 1)
            {
                generatedCurve.curve.Add(new CurvePoint(1.6666667E-05f, network.FluxHistory.Peek().generated));
                consumedCurve.curve.Add(new CurvePoint(1.6666667E-05f, network.FluxHistory.Peek().consumed));
                exceededCurve.curve.Add(new CurvePoint(1.6666667E-05f, network.FluxHistory.Peek().exceeded));
            }

            var section = new FloatRange(minTick, maxTick);
            if (Mathf.Approximately(section.min, section.max))
            {
                section.max += 1.6666667E-05f;
            }

            var curveDrawerStyle = Find.History.curveDrawerStyle;
            curveDrawerStyle.FixedSection = section;
            curveDrawerStyle.YIntegersOnly = true;

            SimpleCurveDrawer.DrawCurves(graphRect, _tmpDrawInfoList, curveDrawerStyle, legendRect: legendRect);
            Text.Anchor = TextAnchor.UpperLeft;
        }
    }
}
