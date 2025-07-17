using Verse;

namespace VVRace
{
    public class MoteViviFairy : Mote
    {
        private Pawn _parent;

        public void Initialize(Pawn parent)
        {
            _parent = parent;
        }
    }
}
