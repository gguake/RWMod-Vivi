using RimWorld;
using System.Collections.Generic;
using Verse;

namespace VVRace
{
    public class RitualOutcomeEffectWorker_WindOfRest : RitualOutcomeEffectWorker_FromQuality
    {
        public RitualOutcomeEffectWorker_WindOfRest() { }
        public RitualOutcomeEffectWorker_WindOfRest(RitualOutcomeEffectDef def) : base(def) { }

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
        }

        public static void Release(ArcanePlant_Everflower everflower)
        {
            var wind = (WindOfRest)ThingMaker.MakeThing(VVThingDefOf.VV_WindOfRest);
            wind.Init(everflower.DrawPos + everflower.EverflowerComp.RitualEffecterOffset);
            GenSpawn.Spawn(wind, everflower.Position, everflower.Map);
        }
    }
}
