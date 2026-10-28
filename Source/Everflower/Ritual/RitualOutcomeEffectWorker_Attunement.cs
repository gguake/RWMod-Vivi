using RimWorld;
using System.Collections.Generic;
using Verse;

namespace VVRace
{
    public class RitualOutcomeEffectWorker_Attunement : RitualOutcomeEffectWorker_FromQuality
    {
        public RitualOutcomeEffectWorker_Attunement() { }
        public RitualOutcomeEffectWorker_Attunement(RitualOutcomeEffectDef def) : base(def) { }

        public override void Apply(float progress, Dictionary<Pawn, int> totalPresence, LordJob_Ritual jobRitual)
        {
            var quality = GetQuality(jobRitual, progress);
            var attuner = jobRitual.PawnWithRole("resonator");
            var everflower = jobRitual.selectedTarget.Thing as ArcanePlant_Everflower;

            // 아포크리파 2페이즈: 조율(링크·잔향) 대신 제단의 공명 파동이 아포크리파를 강타한다.
            // 이 동안에는 의식 재사용 대기가 없으므로 Notify_RitualComplete 대신 순간이동 예약 해제만 한다.
            if (everflower != null && EverflowerRitualRules.IsResonancePhase(everflower))
            {
                ResonanceWaveUtility.Trigger(attuner, everflower);
                everflower.Unreserve();
                return;
            }

            if (attuner != null && everflower != null)
            {
                var map = everflower.Map;
                everflower.EverflowerComp.LinkAttunement(attuner, quality);

                ApplyReverberation(attuner, everflower, map);

                def.effecter.Spawn(everflower, map, everflower.EverflowerComp.RitualEffecterOffset).Cleanup();

                everflower.Notify_RitualComplete(quality);
            }
        }

        private void ApplyReverberation(Pawn attuner, ArcanePlant_Everflower everflower, Map map)
        {
            if (attuner == null || map == null) { return; }

            var severity = everflower.EverflowerComp.AttunementHediffSeverity;
            if (severity <= 0f) { return; }

            foreach (var pawn in map.mapPawns.AllPawnsSpawned)
            {
                if (!pawn.IsVivi() || pawn.IsRoyalVivi() || !pawn.ageTracker.Adult) { continue; }

                var existing = pawn.health.hediffSet.GetFirstHediffOfDef(VVHediffDefOf.VV_EverflowerReverberation);
                if (existing != null)
                {
                    pawn.health.RemoveHediff(existing);
                }

                var hediff = pawn.health.AddHediff(VVHediffDefOf.VV_EverflowerReverberation);
                hediff.Severity = severity;
            }
        }
    }
}
