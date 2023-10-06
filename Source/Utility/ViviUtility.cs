using Verse;

namespace VVRace
{
    public static class ViviUtility
    {
        public static bool IsVivi(this Pawn pawn) => pawn.TryGetComp<CompVivi>() != null;
        public static CompVivi GetCompVivi(this Pawn pawn) => pawn.TryGetComp<CompVivi>();
        public static CompViviEggLayer GetCompViviEggLayer(this Pawn pawn) => pawn.TryGetComp<CompViviEggLayer>();

        public static bool CanLayViviEgg(this Pawn pawn) => pawn.TryGetComp<CompViviEggLayer>()?.CanLayEgg ?? false;

        public static bool IsRoyalVivi(this Pawn pawn) => pawn.TryGetComp<CompVivi>()?.isRoyal ?? false;
    }
}
