using RimWorld;
using System.Collections.Generic;
using Verse;

namespace VVRace
{
    public class RitualOutcomeEffectWorker_Requiem : RitualOutcomeEffectWorker_FromQuality
    {
        public RitualOutcomeEffectWorker_Requiem() { }
        public RitualOutcomeEffectWorker_Requiem(RitualOutcomeEffectDef def) : base(def) { }

        public override void Apply(float progress, Dictionary<Pawn, int> totalPresence, LordJob_Ritual jobRitual)
        {
            var quality = GetQuality(jobRitual, progress);
            var everflower = jobRitual.selectedTarget.Thing as ArcanePlant_Everflower;
            if (everflower == null) { return; }

            if (everflower.Spawned)
            {
                Release(everflower);
                def.effecter?.Spawn(everflower, everflower.Map).Cleanup();
            }

            everflower.Notify_RitualComplete(quality);
            ViviEndingQuest.Current?.Notify_EverflowerRitualCompleted(everflower, jobRitual.PawnWithRole("resonator"));
        }

        public static void Release(ArcanePlant_Everflower everflower)
        {
            var wave = (RequiemWave)ThingMaker.MakeThing(VVThingDefOf.VV_RequiemWave);
            wave.Init(everflower.DrawPos + everflower.EverflowerComp.RitualEffecterOffset);
            GenSpawn.Spawn(wave, everflower.Position, everflower.Map);
        }
    }
}
