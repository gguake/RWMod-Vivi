using RimWorld;
using Verse;

namespace VVRace
{
    public class HediffCompProperties_ChangeRoyalJellyNeed : HediffCompProperties
    {
        public float percentPerDay;

        public HediffCompProperties_ChangeRoyalJellyNeed()
        {
            compClass = typeof(HediffComp_ChangeRoyalJellyNeed);
        }
    }

    public class HediffComp_ChangeRoyalJellyNeed : HediffComp
    {
        private Need _needCached;
        public Need Need
        {
            get
            {
                if (_needCached == null)
                {
                    _needCached = base.Pawn.needs.TryGetNeed(VVNeedDefOf.VV_RoyalJelly);
                }

                return _needCached;
            }
        }

        public HediffCompProperties_ChangeRoyalJellyNeed Props => (HediffCompProperties_ChangeRoyalJellyNeed)props;

        public override void CompPostTick(ref float severityAdjustment)
        {
            var need = Need;
            if (need != null)
            {
                need.CurLevelPercentage += Props.percentPerDay / 60000f * (Pawn.ageTracker.BiologicalTicksPerTick);
            }
        }
    }
}
