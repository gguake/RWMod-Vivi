using Verse;

namespace VVRace
{
    public class ArcanePlant_RadiantFlower : ArcanePlant, IConditionalGraphicProvider
    {
        public int GraphicIndex => CompGlower.Glows ? 1 : 0;

        private CompGlowerArcanePlant _compGlower;
        public CompGlowerArcanePlant CompGlower
        {
            get
            {
                if (_compGlower == null)
                {
                    _compGlower = this.TryGetComp<CompGlowerArcanePlant>();
                }

                return _compGlower;
            }
        }

        private bool? _lastCompGlowerState = null;

        protected override void TickInterval(int delta)
        {
            if (!Spawned || Destroyed)
            {
                return;
            }

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
