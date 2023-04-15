using RimWorld;
using System.Collections.Generic;
using System.Linq;
using Verse;

namespace VVRace
{
    public class MainTabWindow_Vivis : MainTabWindow_PawnTable
    {
        protected override PawnTableDef PawnTableDef => VVPawnTableDefOf.VV_Vivis;

        protected override IEnumerable<Pawn> Pawns => Find.CurrentMap.mapPawns
            .PawnsInFaction(Faction.OfPlayer)
            .Where(p => p.Spawned && p.IsMindLinkedVivi());
    }
}
