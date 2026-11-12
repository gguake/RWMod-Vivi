using RimWorld;
using System.Collections.Generic;
using Verse;

namespace VVRace
{
    public class RitualOutcomeEffectWorker_WindOfRest : RitualOutcomeEffectWorker_EndingEverflower
    {
        public RitualOutcomeEffectWorker_WindOfRest() { }
        public RitualOutcomeEffectWorker_WindOfRest(RitualOutcomeEffectDef def) : base(def) { }

        protected override void Apply(ArcanePlant_Everflower everflower, float quality, LordJob_Ritual jobRitual)
        {
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
