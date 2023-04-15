using Verse;

namespace VVRace
{
    public class PawnKindDef_Vivi : PawnKindDef
    {
        public bool isRoyal;
        public bool preventRoyalBodyType;

        public ViviSpecializationDef initialSpecializationDef;
        public IntRange initialSpecializationTicks = new IntRange(0, 0);
    }
}
