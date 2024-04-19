using RimWorld;
using Verse;

namespace VVRace
{
    public class Book_Vivi : Book
    {
        public override void PostQualitySet()
        {
            var pawnKind = VVPawnKindDefOf.VV_RoyalVivi;
            var faction = FactionUtility.DefaultFactionFrom(pawnKind.defaultFactionType);
            var author = PawnGenerator.GeneratePawn(pawnKind, faction);

            GenerateBook(author);
        }
    }
}
