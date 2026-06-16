using Verse;

namespace VVRace
{
    // 경호 대상에 부착되는 마커 헤디프. 보호 해제 Gizmo(Harmony)가 이 정보로 세션을 종료한다.
    public class Hediff_FairyGuarded : HediffWithComps
    {
        public Pawn ownerVivi;
        public int sessionId;

        public override bool ShouldRemove => ownerVivi == null || ownerVivi.Dead || !ownerVivi.Spawned;

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_References.Look(ref ownerVivi, "ownerVivi");
            Scribe_Values.Look(ref sessionId, "sessionId");
        }
    }
}
