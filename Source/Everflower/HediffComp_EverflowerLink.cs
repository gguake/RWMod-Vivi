using Verse;

namespace VVRace
{
    public class HediffComp_EverflowerLink : HediffComp
    {
        [Unsaved]
        private CompVivi _compVivi;
        [Unsaved]
        private bool _compViviChecked;

        public override bool CompShouldRemove
        {
            get
            {
                if (!_compViviChecked)
                {
                    _compViviChecked = true;
                    _compVivi = Pawn.GetCompVivi();
                }

                if (_compVivi == null || !_compVivi.AttunementActive) { return true; }

                return false;
            }
        }

        public override string CompLabelInBracketsExtra
        {
            get
            {
                var str = (parent.Severity - (int)parent.Severity).ToStringPercent();
                return str;
            }
        }
    }
}
