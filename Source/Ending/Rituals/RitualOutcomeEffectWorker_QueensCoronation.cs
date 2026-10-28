using RimWorld;
using System.Collections.Generic;
using Verse;

namespace VVRace
{
    // 엔딩의 마지막 의식 〈여왕의 대관식〉: 아포크리파를 토벌해 응축이 100%가 된 앵커 영원꽃에서 공명자(로열 비비)가 여왕으로 즉위한다.
    // 의식 결과와 관계없이 영원꽃이 6단계로 완전히 개화하고, 연출과 함께 곧이어 엔딩 크레딧이 흐른다.
    public class RitualOutcomeEffectWorker_QueensCoronation : RitualOutcomeEffectWorker_FromQuality
    {
        public RitualOutcomeEffectWorker_QueensCoronation() { }
        public RitualOutcomeEffectWorker_QueensCoronation(RitualOutcomeEffectDef def) : base(def) { }

        public override void Apply(float progress, Dictionary<Pawn, int> totalPresence, LordJob_Ritual jobRitual)
        {
            var quality = GetQuality(jobRitual, progress);
            var everflower = jobRitual.selectedTarget.Thing as ArcanePlant_Everflower;
            if (everflower == null) { return; }

            var queen = jobRitual.PawnWithRole("resonator");
            if (queen != null && queen.Spawned)
            {
                def.effecter?.Spawn(queen, queen.Map).Cleanup();
            }

            everflower.Notify_RitualComplete(quality);
            ViviEndingQuest.Current?.Notify_CoronationCompleted(everflower, queen);
        }
    }
}
