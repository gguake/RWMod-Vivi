using RimWorld;
using Verse;

namespace VVRace
{
    public class CompProperties_GlowerArcanePlant : CompProperties_Glower
    {
        public bool glowOnlyRoofed;

        public CompProperties_GlowerArcanePlant() : base()
        {
            compClass = typeof(CompGlowerArcanePlant);
        }
    }

    public class CompGlowerArcanePlant : CompGlower
    {
        private new CompProperties_GlowerArcanePlant Props => (CompProperties_GlowerArcanePlant)props;

        private ArcanePlant _cached;
        public ArcanePlant ArcanePlant
        {
            get
            {
                if (_cached == null)
                {
                    _cached = parent as ArcanePlant;
                }

                return _cached;
            }
        }

        protected override bool ShouldBeLitNow
        {
            get
            {
                if (!base.ShouldBeLitNow) { return false; }

                if (ArcanePlant == null) { return false; }

                if (Props.glowOnlyRoofed && !parent.Position.Roofed(parent.Map)) { return false; }

                return ArcanePlant.ManaChargeRatio > 0f;
            }
        }
    }
}
