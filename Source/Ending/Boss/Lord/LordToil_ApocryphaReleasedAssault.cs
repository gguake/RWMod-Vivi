namespace VVRace
{
    public class LordToil_ApocryphaReleasedAssault : LordToil_HornetAssault
    {
        public override bool ShouldFail => lord.ownedPawns.Count == 0;
    }
}
