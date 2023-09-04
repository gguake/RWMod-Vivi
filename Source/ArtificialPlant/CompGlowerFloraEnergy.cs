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

        private ArtificialPlant _cachedArtificialPlant;
        public ArtificialPlant ArtificialPlant
        {
            get
            {
                if (_cachedArtificialPlant == null)
                {
                    _cachedArtificialPlant = parent as ArtificialPlant;
                }

                return _cachedArtificialPlant;
            }
        }

        protected override bool ShouldBeLitNow
        {
            get
            {
                if (!base.ShouldBeLitNow) { return false; }

                if (ArtificialPlant == null) { return false; }

                if (Props.glowOnlyRoofed && !parent.Position.Roofed(parent.Map)) { return false; }

                return ArtificialPlant.EnergyChargeRatio > 0f;
            }
        }
    }
}
