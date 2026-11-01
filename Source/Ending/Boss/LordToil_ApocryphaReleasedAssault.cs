namespace VVRace
{
    // 보스 생존 중 유지하던 빈 로드는 PawnLost 알림이 다시 오지 않으므로 직접 정리한다.
    public class LordToil_ApocryphaReleasedAssault : LordToil_HornetAssault
    {
        public override bool ShouldFail => lord.ownedPawns.Count == 0;
    }
}
