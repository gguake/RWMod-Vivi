using RimWorld;
using System.Collections.Generic;
using Verse;

namespace VVRace
{
    // 5단계 대조율 의식: 엔딩 퀘스트의 공성(개화 응축)을 시작하고, 시작되면 제단에 안치된 4단계 영원꽃을 5단계로 개화시킨다.
    public class RitualOutcomeEffectWorker_GrandAttunement : RitualOutcomeEffectWorker_FromQuality
    {
        private const int GrandAttunementLevel = 5;
        private const float CameraShakeMagnitude = 1.5f;

        public RitualOutcomeEffectWorker_GrandAttunement() { }
        public RitualOutcomeEffectWorker_GrandAttunement(RitualOutcomeEffectDef def) : base(def) { }

        public override void Apply(float progress, Dictionary<Pawn, int> totalPresence, LordJob_Ritual jobRitual)
        {
            var quality = GetQuality(jobRitual, progress);
            var everflower = jobRitual.selectedTarget.Thing as ArcanePlant_Everflower;
            if (everflower == null) { return; }

            var map = everflower.Map;
            var compEverflower = everflower.EverflowerComp;

            // 엔딩(〈만개하는 여명〉)이 실제로 시작됐을 때만 5단계로 개화시킨다. 다른 제단의 대조율 의식이 먼저 엔딩을 시작했거나
            // 퀘스트 생성에 실패하면 4단계로 남겨 둔다(앵커가 아닌 5단계 영원꽃이 남거나, 의식을 다시 치를 수 없게 되지 않도록).
            var ending = ViviEndingQuest.Current;
            if (ending == null || !ending.Notify_GrandAttunementCompleted(everflower, quality))
            {
                Messages.Message(LocalizeString_EndingCore.VV_Message_EndingGrandAttunementNotStarted.Translate(), everflower, MessageTypeDefOf.NegativeEvent);
                everflower.Notify_RitualComplete(quality);
                return;
            }

            compEverflower.SetAttunementLevel(GrandAttunementLevel);

            // 레벨이 오른 뒤의 꽃봉오리 위치(5단계 그래픽)에서 터뜨린다.
            def.effecter?.Spawn(everflower, map, compEverflower.RitualEffecterOffset).Cleanup();

            everflower.Notify_RitualComplete(quality);

            if (map != null && map == Find.CurrentMap)
            {
                Find.CameraDriver.shaker.DoShake(CameraShakeMagnitude);
            }
        }
    }
}
