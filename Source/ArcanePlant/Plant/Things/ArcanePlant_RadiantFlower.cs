using Verse;

namespace VVRace
{
    public class ArcanePlant_RadiantFlower : ArcanePlant
    {
        public int GraphicIndex => CompGlower.Glows ? 1 : 0;

        private CompManaGlower _compGlower;
        public CompManaGlower CompGlower
        {
            get
            {
                if (_compGlower == null)
                {
                    _compGlower = this.TryGetComp<CompManaGlower>();
                }

                return _compGlower;
            }
        }

        private bool? _lastCompGlowerState = null;

        protected override void TickInterval(int delta)
        {
            base.TickInterval(delta);

            if (!Spawned || Destroyed)
            {
                return;
            }

            CompGlower.UpdateLit(Map);
        }

        public override int? OverrideGraphicIndex => CompGlower.Glows ? 1 : 0;
    }
}
