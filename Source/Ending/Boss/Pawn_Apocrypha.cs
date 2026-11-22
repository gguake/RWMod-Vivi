using Verse;

namespace VVRace
{
    public class Pawn_Apocrypha : Pawn
    {
        public override bool CanAttackWhenPathingBlocked
        {
            get
            {
                var comp = GetComp<CompApocrypha>();
                if (comp?.Phase != ApocryphaPhase.Asphodel || !comp.IsQuestBossFight) { return base.CanAttackWhenPathingBlocked; }

                if (pather.Moving && pather.WillCollideNextCell)
                {
                    comp.BlastBreach.NotePathBlocked(pather.nextCell);
                }

                return false;
            }
        }
    }
}
