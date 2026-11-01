using RimWorld;
using System.Collections.Generic;
using Verse;

namespace VVRace
{
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

            // 다른 제단이 먼저 엔딩을 시작했거나 퀘스트 생성에 실패하면 4단계를 유지한다.
            // 앵커가 아닌 5단계 영원꽃이 남거나 의식을 재시도할 수 없게 되는 일을 막는다.
            var ending = ViviEndingQuest.Current;
            if (ending == null || !ending.Notify_GrandAttunementCompleted(everflower, quality))
            {
                Messages.Message(LocalizeString_EndingCore.VV_Message_EndingGrandAttunementNotStarted.Translate(), everflower, MessageTypeDefOf.NegativeEvent);
                everflower.Notify_RitualComplete(quality);
                return;
            }

            compEverflower.SetAttunementLevel(GrandAttunementLevel);

            def.effecter?.Spawn(everflower, map, compEverflower.RitualEffecterOffset).Cleanup();

            everflower.Notify_RitualComplete(quality);

            ending.Notify_EverflowerRitualEcho(everflower, jobRitual.PawnWithRole("resonator"));

            if (map != null && map == Find.CurrentMap)
            {
                Find.CameraDriver.shaker.DoShake(CameraShakeMagnitude);
            }
        }
    }
}
