using RimWorld;
using Verse;

namespace VVRace
{
    public class CompProperties_GlowerFlora : CompProperties_Glower
    {
        public bool glowOnlyRoofed;

        public CompProperties_GlowerFlora() : base()
        {
            compClass = typeof(CompGlowerFloraEnergy);
        }
    }

    public class CompGlowerFloraEnergy : CompGlower
    {
        private new CompProperties_GlowerFlora Props => (CompProperties_GlowerFlora)props;

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

                return ArcanePlant.EnergyChargeRatio > 0f;
            }
        }
    }
}
