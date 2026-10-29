namespace VVRace
{
    // 엔딩 퀘스트 코어(서신·공성·평화·보스 등장·엔딩/실패) Keyed 문자열. Languages/*/Keyed/Ending_Core.xml과 짝을 이룬다.
    public static class LocalizeString_EndingCore
    {
        // 퀘스트 탭: 밀서 퀘스트 목표, 본편 퀘스트 이름·설명, 평화 기간 남은 시간({0})
        public const string VV_Quest_EndingMissiveGoals = nameof(VV_Quest_EndingMissiveGoals);
        public const string VV_Quest_EndingName = nameof(VV_Quest_EndingName);
        public const string VV_Quest_EndingDesc = nameof(VV_Quest_EndingDesc);
        public const string VV_Quest_EndingCalmExpiry = nameof(VV_Quest_EndingCalmExpiry);
        public const string VV_Quest_EndingCalmExpiryTip = nameof(VV_Quest_EndingCalmExpiryTip);

        // 비비 연방의 밀서 (사건 → 제단 해금)
        public const string VV_Letter_EndingFederationMissiveLabel = nameof(VV_Letter_EndingFederationMissiveLabel);
        public const string VV_Letter_EndingFederationMissive = nameof(VV_Letter_EndingFederationMissive);

        // 고대 호박 코어 의뢰(통신기, 비비 연방): 선택지, 비활성 사유, 결과·실패 대화({LEADER})
        public const string VV_Comms_AmberCoreRequest = nameof(VV_Comms_AmberCoreRequest);
        public const string VV_Comms_AmberCoreRequestHostile = nameof(VV_Comms_AmberCoreRequestHostile);
        public const string VV_Comms_AmberCoreRequestActive = nameof(VV_Comms_AmberCoreRequestActive);
        public const string VV_Comms_AmberCoreRequestCoreExists = nameof(VV_Comms_AmberCoreRequestCoreExists);
        public const string VV_Comms_AmberCoreRequestResult = nameof(VV_Comms_AmberCoreRequestResult);
        public const string VV_Comms_AmberCoreRequestNoQuest = nameof(VV_Comms_AmberCoreRequestNoQuest);

        // 〈여명의 맥동〉 공성 시작
        public const string VV_Letter_EndingSiegeLabel = nameof(VV_Letter_EndingSiegeLabel);
        public const string VV_Letter_EndingSiege = nameof(VV_Letter_EndingSiege);
        public const string VV_Letter_EndingSiegeFederation = nameof(VV_Letter_EndingSiegeFederation);

        // 연방 지원군
        public const string VV_Letter_EndingFederationAidLabel = nameof(VV_Letter_EndingFederationAidLabel);
        public const string VV_Letter_EndingFederationAid = nameof(VV_Letter_EndingFederationAid);

        // 〈폭풍 전의 고요〉 평화 기간
        public const string VV_Letter_EndingCalmLabel = nameof(VV_Letter_EndingCalmLabel);
        public const string VV_Letter_EndingCalm = nameof(VV_Letter_EndingCalm);
        public const string VV_Letter_EndingCalmGuide = nameof(VV_Letter_EndingCalmGuide);

        // 〈바스러진 여왕, 아포크리파〉 등장
        public const string VV_Letter_EndingApocryphaLabel = nameof(VV_Letter_EndingApocryphaLabel);
        public const string VV_Letter_EndingApocrypha = nameof(VV_Letter_EndingApocrypha);

        // 아포크리파 토벌 → 〈여왕의 대관식〉 대기
        public const string VV_Letter_EndingCoronationLabel = nameof(VV_Letter_EndingCoronationLabel);
        public const string VV_Letter_EndingCoronation = nameof(VV_Letter_EndingCoronation);

        // 〈영원한 봄의 제국〉 엔딩 (편지와 크레딧이 같은 본문을 쓴다)
        public const string VV_Letter_EndingVictoryLabel = nameof(VV_Letter_EndingVictoryLabel);
        public const string VV_Letter_EndingVictoryContinue = nameof(VV_Letter_EndingVictoryContinue);
        public const string VV_EndingCredits = nameof(VV_EndingCredits);
        public const string VV_EndingCreditsNoQueen = nameof(VV_EndingCreditsNoQueen);
        public const string VV_EndingCreditsExiled = nameof(VV_EndingCreditsExiled);
        public const string VV_EndingCreditsRefugee = nameof(VV_EndingCreditsRefugee);
        public const string VV_EndingCreditsOutro = nameof(VV_EndingCreditsOutro);

        // 실패
        public const string VV_Letter_EndingFailureLabel = nameof(VV_Letter_EndingFailureLabel);
        public const string VV_Letter_EndingFailureStolen = nameof(VV_Letter_EndingFailureStolen);
        public const string VV_Letter_EndingFailureLostLabel = nameof(VV_Letter_EndingFailureLostLabel);
        public const string VV_Letter_EndingFailureLost = nameof(VV_Letter_EndingFailureLost);

        // 메시지
        public const string VV_Message_EndingApocryphaDeferred = nameof(VV_Message_EndingApocryphaDeferred);
        public const string VV_Message_EndingPawnReturned = nameof(VV_Message_EndingPawnReturned);
        public const string VV_Message_EndingPawnReturnedToCaravan = nameof(VV_Message_EndingPawnReturnedToCaravan);
        public const string VV_Message_EndingGrandAttunementNotStarted = nameof(VV_Message_EndingGrandAttunementNotStarted);

        // 개화 응축 진행 막대 기즈모
        public const string VV_Gizmo_EndingBloom = nameof(VV_Gizmo_EndingBloom);
        public const string VV_Gizmo_EndingBloomSiege = nameof(VV_Gizmo_EndingBloomSiege);
        public const string VV_Gizmo_EndingBloomCalm = nameof(VV_Gizmo_EndingBloomCalm);
        public const string VV_Gizmo_EndingBloomBlocked = nameof(VV_Gizmo_EndingBloomBlocked);
        public const string VV_Gizmo_EndingBloomCoronation = nameof(VV_Gizmo_EndingBloomCoronation);
        public const string VV_Gizmo_EndingBloomDesc = nameof(VV_Gizmo_EndingBloomDesc);
        public const string VV_Gizmo_EndingBloomDescSiege = nameof(VV_Gizmo_EndingBloomDescSiege);
        public const string VV_Gizmo_EndingBloomDescCalm = nameof(VV_Gizmo_EndingBloomDescCalm);
        public const string VV_Gizmo_EndingBloomDescBlocked = nameof(VV_Gizmo_EndingBloomDescBlocked);
        public const string VV_Gizmo_EndingBloomDescCoronation = nameof(VV_Gizmo_EndingBloomDescCoronation);

        // 영원꽃 정보창
        public const string VV_Inspector_EndingBloom = nameof(VV_Inspector_EndingBloom);
        public const string VV_Inspector_EndingSiege = nameof(VV_Inspector_EndingSiege);
        public const string VV_Inspector_EndingCalm = nameof(VV_Inspector_EndingCalm);
        public const string VV_Inspector_EndingCalmDeferred = nameof(VV_Inspector_EndingCalmDeferred);
        public const string VV_Inspector_EndingBlocked = nameof(VV_Inspector_EndingBlocked);
        public const string VV_Inspector_EndingCoronation = nameof(VV_Inspector_EndingCoronation);
        public const string VV_Inspector_EndingFullBloom = nameof(VV_Inspector_EndingFullBloom);

        // 공명자가 없을 때 {PAWN_labelShort} 자리에 쓰는 대체 문구
        public const string VV_Ending_ResonatorFallback = nameof(VV_Ending_ResonatorFallback);

        // 〈황금빛 꽃안개〉
        public const string VV_Inspector_PlantManaUnlimited = nameof(VV_Inspector_PlantManaUnlimited);
        public const string VV_DialogChangeWeatherGoldenBloomDesc = nameof(VV_DialogChangeWeatherGoldenBloomDesc);
        public const string VV_FailReason_GoldenBloomHoldsWeather = nameof(VV_FailReason_GoldenBloomHoldsWeather);
    }
}
