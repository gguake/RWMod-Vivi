using RimWorld;

namespace VVRace
{
    // 엔딩 보스전 배경음 전환. 퀘스트가 기억하는 아포크리파가 맵에 있는 동안 페이즈에 맞는 곡을 튼다.
    // 토벌·찬탈로 보스가 사라지면 조건이 풀리고, MusicManagerPlay가 곡을 페이드아웃한다(useTransitionForLifetime).
    public abstract class MusicTransition_Apocrypha : MusicTransition
    {
        protected abstract bool PlaysInPhase(ApocryphaPhase phase);

        public override bool IsTransitionSatisfied()
        {
            if (!base.IsTransitionSatisfied()) { return false; }

            var comp = ViviEndingQuest.Current?.BossComp;
            return comp != null && comp.parent.Spawned && comp.IsQuestBossFight && PlaysInPhase(comp.Phase);
        }
    }

    // 1페이즈 〈수십억의 굴레〉
    public class MusicTransition_ApocryphaBound : MusicTransition_Apocrypha
    {
        protected override bool PlaysInPhase(ApocryphaPhase phase) => phase == ApocryphaPhase.Bound;
    }

    // 2페이즈 〈해방된 시조의 폭주〉. 곡 도입부의 충격음이 변신 충격파와 맞도록 변신 연출 시작부터 튼다.
    public class MusicTransition_ApocryphaUnbound : MusicTransition_Apocrypha
    {
        protected override bool PlaysInPhase(ApocryphaPhase phase) => phase == ApocryphaPhase.Transforming || phase == ApocryphaPhase.Unbound;
    }
}
