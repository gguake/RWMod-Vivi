using UnityEngine;
using Verse;

namespace VVRace
{
    public class CompProperties_WaterdropFruit : CompProperties
    {
        public ThingDef fruitDef;
        public FloatRange intervalDays;
        public IntRange count;

        public CompProperties_WaterdropFruit()
        {
            compClass = typeof(CompWaterdropFruit);
        }
    }

    public class CompWaterdropFruit : ThingComp
    {
        public CompProperties_WaterdropFruit Props => (CompProperties_WaterdropFruit)props;
        public ArcanePlant Plant => (ArcanePlant)parent;

        private int _remainingTicks;
        private int _nextMatureTicks;

        public override void PostExposeData()
        {
            Scribe_Values.Look(ref _remainingTicks, "remainingFruitTicks");
            Scribe_Values.Look(ref _nextMatureTicks, "nextFruitMatureTicks");
        }

        public override void PostPostMake()
        {
            _nextMatureTicks = Mathf.CeilToInt(Props.intervalDays.RandomInRange * 60000f);
            _remainingTicks = _nextMatureTicks + _remainingTicks;
        }

        public override void CompTickInterval(int delta)
        {
            if (parent.Spawned)
            {
                if (Plant.ManaComp.Active)
                {
                    _remainingTicks -= delta;
                    if (_remainingTicks <= 0)
                    {
                        _nextMatureTicks = Mathf.CeilToInt(Props.intervalDays.RandomInRange * 60000f);
                        _remainingTicks = _nextMatureTicks + _remainingTicks;

                        var thing = ThingMaker.MakeThing(Props.fruitDef);
                        thing.stackCount = Props.count.RandomInRange;
                        if (!GenPlace.TryPlaceThing(thing, parent.Position, parent.Map, ThingPlaceMode.Near))
                        {
                            thing.Destroy();
                        }
                    }
                }
            }
        }

        public override string CompInspectStringExtra()
        {
            var pct = _nextMatureTicks > 0 ? Mathf.Clamp01(1f - (float)_remainingTicks / _nextMatureTicks) : 0f;
            return $"{Props.fruitDef.LabelCap}: {pct.ToStringPercent()}";
        }
    }
}
