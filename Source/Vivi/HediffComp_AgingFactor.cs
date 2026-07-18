using RimWorld;
using System.Collections.Generic;
using Verse;

namespace VVRace
{
    public class HediffCompProperties_AgingFactor : HediffCompProperties
    {
        public float factor = 1f;

        public HediffCompProperties_AgingFactor()
        {
            compClass = typeof(HediffComp_AgingFactor);
        }
    }

    public class HediffComp_AgingFactor : HediffComp
    {
        private static readonly HashSet<int> _cache = new HashSet<int>();
        private static Game _cachedGame;

        public HediffCompProperties_AgingFactor Props => (HediffCompProperties_AgingFactor)props;

        public override string CompTipStringExtra
            => LocalizeString_Etc.VV_Hediff_AgingFactor.Translate(Props.factor.ToStringPercentEmptyZero());

        public override void CompPostPostAdd(DamageInfo? dinfo)
        {
            _cache.Add(Pawn.thingIDNumber);
        }

        public override void CompPostPostRemoved()
        {
            _cache.Remove(Pawn.thingIDNumber);
        }

        public static float ApplyAgingFactor(float factor, Pawn_GeneTracker geneTracker)
        {
            var pawn = geneTracker?.pawn;
            if (pawn == null) { return factor; }

            if (_cachedGame != Current.Game)
            {
                _cache.Clear();
                _cachedGame = Current.Game;
            }

            if (_cache.Contains(pawn.thingIDNumber))
            {
                var hediffFactor = 1f;
                var hediffs = pawn.health?.hediffSet?.hediffs;
                if (hediffs != null)
                {
                    for (int i = 0; i < hediffs.Count; ++i)
                    {
                        if (hediffs[i] is HediffWithComps hediffWithComps && hediffWithComps.comps != null)
                        {
                            var comps = hediffWithComps.comps;
                            for (int j = 0; j < comps.Count; ++j)
                            {
                                if (comps[j] is HediffComp_AgingFactor agingFactorComp)
                                {
                                    hediffFactor *= agingFactorComp.Props.factor;
                                }
                            }
                        }
                    }
                }

                return factor * hediffFactor;
            }
            else
            {
                return factor;
            }
        }
    }
}
