using RimWorld;
using Verse;

namespace VVRace
{
    // 5단계 〈대조율 의식〉 행동 워커. 공명자가 영원꽃 곁에 도착해 의식이 진행되기 시작하면(두 번째 단계) 공명자를 〈영원꽃 완전 동화〉 상태로 만든다.
    // 동화는 의식이 끝나 엔딩이 시작되면 아포크리파 토벌까지 이어지고, 엔딩이 시작되지 않고 의식이 끝나면 풀린다(Hediff_EverflowerAssimilation).
    // 공명자가 쓰러지거나 죽으면 바닐라가 의식을 취소한다(필수 역할의 폰이 로드를 떠남).
    public class RitualBehaviorWorker_GrandAttunement : RitualBehaviorWorker_GeneralEverflowerRitual
    {
        private const int AssimilationStageIndex = 1;
        private const string ResonatorRoleId = "resonator";

        public RitualBehaviorWorker_GrandAttunement() { }
        public RitualBehaviorWorker_GrandAttunement(RitualBehaviorDef def) : base(def) { }

        public override void Tick(LordJob_Ritual ritual)
        {
            base.Tick(ritual);

            if (ritual.StageIndex < AssimilationStageIndex) { return; }

            var resonator = ritual.PawnWithRole(ResonatorRoleId);
            if (resonator == null || !resonator.Spawned || resonator.Dead || resonator.Downed) { return; }
            if (Hediff_EverflowerAssimilation.IsAssimilated(resonator)) { return; }

            ViviEndingQuest.Current?.BeginAssimilation(resonator, ritual.selectedTarget.Thing as ArcanePlant_Everflower);
        }
    }
}
