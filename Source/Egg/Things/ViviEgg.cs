using Verse;

namespace VVRace
{
    public class ViviEgg : ThingWithComps, IConditionalGraphicProvider
    {
        public CompViviHatcher CompViviHatcher
        {
            get
            {
                if (_compViviHatcher == null)
                {
                    _compViviHatcher = this.TryGetComp<CompViviHatcher>();
                }

                return _compViviHatcher;
            }
        }
        private CompViviHatcher _compViviHatcher;

        private int _seed;

        public int GraphicIndex
        {
            get
            {
                var hatcherComp = CompViviHatcher;
                if (hatcherComp != null)
                {
                    if (hatcherComp.hatchProgress >= 0.7f)
                    {
                        return 2;
                    }
                    else if (hatcherComp.hatchProgress >= 0.3f)
                    {
                        return 1;
                    }
                }

                return 0;
            }
        }

        public override void ExposeData()
        {
            base.ExposeData();

            Scribe_Values.Look(ref _seed, "seed");

            if (Scribe.mode == LoadSaveMode.LoadingVars)
            {
                TryRegenerateSeed();
            }
        }

        public override void PostMake()
        {
            base.PostMake();
            TryRegenerateSeed();
        }

        public override bool CanStackWith(Thing other)
        {
            return false;
        }

        public override bool TryAbsorbStack(Thing other, bool respectStackLimit)
        {
            return false;
        }

        private void TryRegenerateSeed()
        {
            if (_seed == 0)
            {
                _seed = Rand.RangeInclusive(1, int.MaxValue);
            }
        }
    }
}
