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
        private static readonly Dictionary<int, float> _cache = new Dictionary<int, float>();
        private static Game _cachedGame;

        public HediffCompProperties_AgingFactor Props => (HediffCompProperties_AgingFactor)props;

        public override string CompTipStringExtra
            => LocalizeString_Etc.VV_Hediff_AgingFactor.Translate(Props.factor.ToStringPercentEmptyZero());

        public override void CompPostPostAdd(DamageInfo? dinfo)
        {
            InvalidateCache();
        }

        public override void CompPostPostRemoved()
        {
            InvalidateCache();
        }

        public override void CompExposeData()
        {
            base.CompExposeData();
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                InvalidateCache();
            }
        }

        private void InvalidateCache()
        {
            InvalidateCache(Pawn);
        }

        public static void InvalidateCache(Pawn pawn)
        {
            EnsureCachedGame();
            if (pawn != null)
            {
                _cache.Remove(pawn.thingIDNumber);
            }
        }

        private static void EnsureCachedGame()
        {
            if (_cachedGame != Current.Game)
            {
                _cache.Clear();
                _cachedGame = Current.Game;
            }
        }

        public static float ApplyAgingFactor(float factor, Pawn_GeneTracker geneTracker)
        {
            var pawn = geneTracker?.pawn;
            if (pawn == null) { return factor; }

            EnsureCachedGame();
            if (_cache.TryGetValue(pawn.thingIDNumber, out var cachedFactor))
            {
                return factor * cachedFactor;
            }

            // 불러올 때는 CompPostPostAdd가 호출되지 않으므로 해당 폰의 캐시 항목이 없어도 헤디프를 직접 확인한다.
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

            if (Scribe.mode == LoadSaveMode.Inactive)
            {
                _cache[pawn.thingIDNumber] = hediffFactor;
            }
            return factor * hediffFactor;
        }
    }
}
