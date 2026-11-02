using RimWorld;
using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace VVRace
{
    public class RitualOutcomeEffectDef_SkyShield : RitualOutcomeEffectDef
    {
        public ThingDef shieldDef;
        public SimpleCurve durationTicksByQuality;
    }

    public class RitualOutcomeEffectWorker_SkyShield : RitualOutcomeEffectWorker_FromQuality
    {
        public RitualOutcomeEffectDef_SkyShield Def => (RitualOutcomeEffectDef_SkyShield)def;

        public RitualOutcomeEffectWorker_SkyShield() { }
        public RitualOutcomeEffectWorker_SkyShield(RitualOutcomeEffectDef def) : base(def) { }

        public override void Apply(float progress, Dictionary<Pawn, int> totalPresence, LordJob_Ritual jobRitual)
        {
            var quality = GetQuality(jobRitual, progress);
            var everflower = jobRitual.selectedTarget.Thing as ArcanePlant_Everflower;
            if (everflower == null) { return; }

            if (everflower.Spawned)
            {
                RaiseShield(everflower, Mathf.RoundToInt(Def.durationTicksByQuality.Evaluate(quality)));
                def.effecter?.Spawn(everflower, everflower.Map).Cleanup();
            }

            everflower.Notify_RitualComplete(quality);
        }

        public void RaiseShield(ArcanePlant_Everflower everflower, int durationTicks)
        {
            var map = everflower.Map;
            var shield = ViviShieldDome.FindFor(map, Def.shieldDef, everflower);
            if (shield != null)
            {
                shield.ExtendTo(durationTicks);
            }
            else
            {
                shield = (ViviShieldDome)ThingMaker.MakeThing(Def.shieldDef);
                shield.Init(everflower, durationTicks);
                GenSpawn.Spawn(shield, everflower.Position, map);
            }

            Messages.Message(
                LocalizeString_Message.VV_Message_SkyShieldRaised.Translate(shield.TicksLeft.ToStringTicksToPeriod()),
                everflower,
                MessageTypeDefOf.PositiveEvent);
        }
    }
}
