using Verse;

namespace VVRace
{
    // 〈여왕의 대관식〉을 마친 비비의 권능. 능력(선고·마나 보호막)은 HediffDef.abilities로 주고 생명 공명은 사망 통지로 처리한다.
    public class Hediff_EverflowerQueen : HediffWithComps
    {
        public override void Notify_PawnDied(DamageInfo? dinfo, Hediff culprit = null)
        {
            base.Notify_PawnDied(dinfo, culprit);
            ViviEndingQuest.Current?.Notify_QueenDied(pawn);
        }
    }
}
