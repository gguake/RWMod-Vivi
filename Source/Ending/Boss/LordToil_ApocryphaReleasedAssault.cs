namespace VVRace
{
    // 보스가 사라진 뒤 호위·꽃 경비 말벌이 넘어가는 일반 습격 토일. 말벌이 하나도 남지 않으면 로드를 없앤다
    // (보스가 살아 있는 동안 빈 로드를 유지하므로, 그 뒤에는 폰을 잃는 알림이 다시 오지 않아 스스로 정리되지 않는다).
    public class LordToil_ApocryphaReleasedAssault : LordToil_HornetAssault
    {
        public override bool ShouldFail => lord.ownedPawns.Count == 0;
    }
}
