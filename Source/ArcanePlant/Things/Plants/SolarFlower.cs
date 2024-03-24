using Verse;

namespace VVRace
{
    public class SolarFlower : ArcanePlant, IConditionalGraphicProvider
    {
        public int GraphicIndex => CompGlower.Glows ? 1 : 0;

        private CompGlowerFloraEnergy _compGlower;
        public CompGlowerFloraEnergy CompGlower
        {
            get
            {
                if (_compGlower == null)
                {
                    _compGlower = this.TryGetComp<CompGlowerFloraEnergy>();
                }

                return _compGlower;
            }
        }

        private bool? _lastCompGlowerState = null;

        public override void Tick()
        {
            base.Tick();

            UpdateGraphic();
        }

        public override void TickRare()
        {
            base.TickRare();

            UpdateGraphic();
        }

        public override void TickLong()
        {
            base.TickLong();

            UpdateGraphic();
        }

        private void UpdateGraphic()
        {
            if (_lastCompGlowerState != CompGlower.Glows)
            {
                _lastCompGlowerState = CompGlower.Glows;

                DirtyMapMesh(Map);
            }
        }
    }
}
