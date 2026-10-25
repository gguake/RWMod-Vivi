using RimWorld;
using System.Linq;
using UnityEngine;
using Verse;

namespace VVRace
{
    // 영원꽃 공명 레벨에 따라 명상 초점 강도를 더한다.
    public class FocusStrengthOffset_EverflowerLevel : FocusStrengthOffset_Curve
    {
        protected override string ExplanationKey => LocalizeString_Stat.VV_StatsReport_EverflowerAttunementLevel;

        protected override float SourceValue(Thing parent)
        {
            return parent is ArcanePlant_Everflower everflower ? everflower.AttunementLevel : 0;
        }
    }

    // 명상하는 폰이 이 영원꽃과 공명 중이면 공명 레벨에 따른 보너스를 더한다.
    public class FocusStrengthOffset_EverflowerResonator : FocusStrengthOffset
    {
        public SimpleCurve curve;

        public override bool DependsOnPawn => true;

        public override bool CanApply(Thing parent, Pawn user = null)
        {
            if (user == null || !(parent is ArcanePlant_Everflower everflower)) { return false; }

            return everflower.EverflowerComp.LinkedPawns.Contains(user);
        }

        public override float GetOffset(Thing parent, Pawn user = null)
        {
            var level = parent is ArcanePlant_Everflower everflower ? everflower.AttunementLevel : 0;
            return Mathf.Round(curve.Evaluate(level) * 100f) / 100f;
        }

        public override string GetExplanation(Thing parent)
        {
            return LocalizeString_Stat.VV_StatsReport_EverflowerResonator.Translate() + ": " + GetOffset(parent).ToStringWithSign("0%");
        }

        public override string GetExplanationAbstract(ThingDef def = null)
        {
            return LocalizeString_Stat.VV_StatsReport_EverflowerResonator.Translate() + ": "
                + curve[0].y.ToStringWithSign("0%") + " " + "RangeTo".Translate() + " " + curve[curve.PointsCount - 1].y.ToStringWithSign("0%");
        }

        public override float MaxOffset(Thing parent = null)
        {
            var max = 0f;
            for (int i = 0; i < curve.PointsCount; ++i)
            {
                if (Mathf.Abs(curve[i].y) > Mathf.Abs(max))
                {
                    max = curve[i].y;
                }
            }
            return max;
        }
    }
}
