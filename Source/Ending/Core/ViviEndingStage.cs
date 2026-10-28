namespace VVRace
{
    // 엔딩 퀘스트 〈만개하는 여명〉 진행 단계. 세이브에 저장되므로 값을 바꾸거나 순서를 바꾸지 말 것.
    // 진행 순서: None → LetterPending → AltarUnlocked → Siege → Calm → Boss → Coronation → Completed (또는 Failed).
    // Coronation은 나중에 추가되어 값이 진행 순서와 다르므로 단계끼리 대소 비교를 하지 말 것.
    public enum ViviEndingStage : byte
    {
        None = 0,           // 영원꽃 4단계 미달
        LetterPending = 1,  // 4단계 도달, 비비 연방 서신 대기
        AltarUnlocked = 2,  // 서신 수신: 제단 연구/고대 호박 코어 입수 가능
        Siege = 3,          // 5단계 대조율 완료: 개화 응축 0→99%, 장기 공성
        Calm = 4,           // 99% 도달: 짧은 평화 기간
        Boss = 5,           // 아포크리파 출현 중 (응축 99% 고정)
        Completed = 6,      // 〈여왕의 대관식〉 완료 → 6단계 완전 개화
        Failed = 7,         // 영원꽃 찬탈 → 최종 실패
        Coronation = 8,     // 아포크리파 토벌 → 응축 100%, 〈여왕의 대관식〉 대기 (완료하면 Completed)
    }
}
