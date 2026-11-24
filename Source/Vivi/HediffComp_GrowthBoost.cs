using Verse;

namespace VVRace
{
    public class HediffCompProperties_GrowthBoost : HediffCompProperties
    {
        public HediffCompProperties_GrowthBoost()
        {
            compClass = typeof(HediffComp_GrowthBoost);
        }
    }

    public class HediffComp_GrowthBoost : HediffComp
    {
        [Unsaved]
        private CompVivi _compVivi;
        [Unsaved]
        private bool _compViviChecked;

        public override bool CompShouldRemove
        {
            get
            {
                if (Pawn.DevelopmentalStage.Adult()) { return true; }

                if (!_compViviChecked)
                {
                    _compViviChecked = true;
                    _compVivi = Pawn.GetCompVivi();
                }

                return _compVivi == null;
            }
        }
    }
}
