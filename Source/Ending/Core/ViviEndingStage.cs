namespace VVRace
{
    // 엔딩 퀘스트 〈만개하는 여명〉 진행 단계. 본편 퀘스트의 상태 부품(QuestPart_ViviEndingState)이 저장한다(Scribe_Values는 이름으로 저장하므로 값은 세이브와 무관하다).
    // None~AltarUnlocked는 창구(ViviEndingQuest)가 서신 예약·밀서 퀘스트 유무로, Completed/Failed는 본편 퀘스트의 종료 결과로도 판단한다.
    // 진행 순서: None → LetterPending → AltarUnlocked → Siege → Calm → Boss → Coronation → Completed (또는 Failed).
    // 값은 진행 순서를 따르므로 단계끼리 대소 비교를 해도 된다(Failed는 맨 뒤).
    public enum ViviEndingStage : byte
    {
        None = 0,           // 영원꽃 4단계 미달
        LetterPending = 1,  // 4단계 도달, 비비 연방 서신 대기
        AltarUnlocked = 2,  // 서신 수신: 제단 연구/고대 호박 코어 입수 가능
        Siege = 3,          // 5단계 대조율 완료: 개화 응축 0→99%, 장기 공성
        Calm = 4,           // 99% 도달: 짧은 평화 기간
        Boss = 5,           // 아포크리파 출현 중 (응축 99% 고정)
        Coronation = 6,     // 아포크리파 토벌 → 응축 100%, 〈여왕의 대관식〉 대기 (완료하면 Completed)
        Completed = 7,      // 〈여왕의 대관식〉 완료 → 6단계 완전 개화
        Failed = 8,         // 영원꽃 찬탈·소실 → 최종 실패
    }

    public static class ViviEndingStageUtility
    {
        // 엔딩이 진행 중인 단계(공성~대관식 대기). 이 동안 앵커를 잃으면 실패한다.
        public static bool IsActiveStage(this ViviEndingStage stage)
        {
            return stage >= ViviEndingStage.Siege && stage <= ViviEndingStage.Coronation;
        }

        // 엔딩이 끝난 단계(완료·실패).
        public static bool IsFinishedStage(this ViviEndingStage stage)
        {
            return stage == ViviEndingStage.Completed || stage == ViviEndingStage.Failed;
        }
    }
}
