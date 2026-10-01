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
        private static readonly Dictionary<int, bool> _cache = new Dictionary<int, bool>();
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
            EnsureCachedGame();
            if (Pawn != null)
            {
                _cache.Remove(Pawn.thingIDNumber);
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
            if (_cache.TryGetValue(pawn.thingIDNumber, out var hasAgingFactor) && !hasAgingFactor)
            {
                return factor;
            }

            // Cache misses must inspect loaded hediffs: loading does not call CompPostPostAdd.
            hasAgingFactor = false;
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
                                hasAgingFactor = true;
                                hediffFactor *= agingFactorComp.Props.factor;
                            }
                        }
                    }
                }
            }

            _cache[pawn.thingIDNumber] = hasAgingFactor;
            return factor * hediffFactor;
        }
    }
}
