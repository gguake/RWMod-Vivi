﻿using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;

namespace VVRace
{
    public class ManaFluxRule_RadialPollution : ManaFluxRule
    {
        public float radius;
        public SimpleCurve manaCurveFromDistanceSqr;

        protected List<(IntVec3 v, float mana)> _radialCellAndManaCache = null;
        public IEnumerable<(IntVec3 v, float mana)> RadialCellAndMana
        {
            get
            {
                if (_radialCellAndManaCache == null)
                {
                    _radialCellAndManaCache = new List<(IntVec3 v, float mana)>();
                    foreach (var cell in GenRadial.RadialPatternInRadius(radius))
                    {
                        var distanceSqr = cell.DistanceToSquared(IntVec3.Zero);
                        _radialCellAndManaCache.Add((cell, manaCurveFromDistanceSqr.Evaluate(distanceSqr)));
                    }
                }

                return _radialCellAndManaCache;
            }
        }

        public override IntRange ApproximateManaFlux
        {
            get
            {
                var manaSum = Mathf.FloorToInt(RadialCellAndMana.Sum(v => v.mana));
                return new IntRange(0, manaSum);
            }
        }

        public override string GetRuleString(bool inverse) =>
            LocalizeString_Stat.VV_StatsReport_ManaFluxRule_RadialPollution_Desc.Translate(
                (inverse ? -manaCurveFromDistanceSqr.MinY : manaCurveFromDistanceSqr.MinY).ToString("+0;-#"),
                (inverse ? -manaCurveFromDistanceSqr.MaxY : manaCurveFromDistanceSqr.MaxY).ToString("+0;-#"));

        public override int CalcManaFlux(ManaAcceptor manaAcceptor)
        {
            var manaSum = 0f;
            foreach (var tuple in RadialCellAndMana)
            {
                var map = manaAcceptor.Map;
                var cell = manaAcceptor.Position + tuple.v;
                if (cell.InBounds(map) && !cell.Impassable(map) && cell.IsPolluted(map))
                {
                    manaSum += tuple.mana;
                }
            }

            return Mathf.FloorToInt(manaSum);
        }
    }
}
