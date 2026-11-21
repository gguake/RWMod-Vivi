using RimWorld;
using System.Collections.Generic;
using Verse;

namespace VVRace
{
    public class RitualOutcomeEffectWorker_GrandAttunement : RitualOutcomeEffectWorker_EndingEverflower
    {
        public const int GrandAttunementLevel = 5;
        public const int MinViviParticipants = 10;
        private const float CameraShakeMagnitude = 1.5f;

        public RitualOutcomeEffectWorker_GrandAttunement() { }
        public RitualOutcomeEffectWorker_GrandAttunement(RitualOutcomeEffectDef def) : base(def) { }

        public override IEnumerable<string> BlockingIssues(Precept_Ritual ritual, TargetInfo target, RitualRoleAssignments assignments)
        {
            foreach (var issue in base.BlockingIssues(ritual, target, assignments))
            {
                yield return issue;
            }

            if (CountViviParticipants(assignments) < MinViviParticipants)
            {
                yield return LocalizeString_Etc.VV_FailReason_GrandAttunementMinVivis.Translate(MinViviParticipants);
            }
        }

        public static int CountViviParticipants(RitualRoleAssignments assignments)
        {
            if (assignments == null) { return 0; }

            var count = 0;
            var spectators = assignments.SpectatorsForReading;
            for (int i = 0; i < spectators.Count; ++i)
            {
                if (spectators[i] != null && spectators[i].IsVivi())
                {
                    ++count;
                }
            }

            return count;
        }

        protected override void Apply(ArcanePlant_Everflower everflower, float quality, LordJob_Ritual jobRitual)
        {
            var map = everflower.Map;
            var compEverflower = everflower.EverflowerComp;

            var ending = ViviEndingQuest.Current;
            if (ending == null || !ending.Notify_GrandAttunementCompleted(everflower, quality))
            {
                everflower.Notify_RitualComplete(quality);
                return;
            }

            compEverflower.SetAttunementLevel(GrandAttunementLevel, sendLetter: false);

            def.effecter?.Spawn(everflower, map, compEverflower.RitualEffecterOffset).Cleanup();

            everflower.Notify_RitualComplete(quality);

            ApocryphaUtility.ShakeCamera(map, CameraShakeMagnitude);
        }
    }
}
