using RimWorld;
using Verse;

namespace VVRace
{
    public class RitualSpectatorFilter_Vivi : RitualSpectatorFilter
    {
        public override bool Allowed(Pawn p)
        {
            return p != null && p.IsVivi();
        }
    }
}
