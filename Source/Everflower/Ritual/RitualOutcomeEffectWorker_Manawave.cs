using RimWorld;
using System.Collections.Generic;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace VVRace
{
    public class RitualOutcomeEffectDef_ManaWave : RitualOutcomeEffectDef
    {
        public SimpleCurve manaByQuality;
        public SimpleCurve stunDamageByQuality;
        public SimpleCurve incidentScaleByQuality;
    }

    public class RitualOutcomeEffectWorker_Manawave : RitualOutcomeEffectWorker_FromQuality
    {
        private const float RadiusSqr = 55f * 55f;
        public RitualOutcomeEffectDef_ManaWave Def => (RitualOutcomeEffectDef_ManaWave)def;

        public RitualOutcomeEffectWorker_Manawave() { }
        public RitualOutcomeEffectWorker_Manawave(RitualOutcomeEffectDef def) : base(def) { }

        public override void Apply(float progress, Dictionary<Pawn, int> totalPresence, LordJob_Ritual jobRitual)
        {
            var quality = GetQuality(jobRitual, progress);
            var baseMana = Def.manaByQuality.Evaluate(quality);
            var stunDamage = Mathf.CeilToInt(Def.stunDamageByQuality.Evaluate(quality));
            var incidentScale = Def.incidentScaleByQuality.Evaluate(quality);

            var everflower = jobRitual.selectedTarget.Thing as ArcanePlant_Everflower;
            var map = everflower.Map;
            var manaComponent = map.GetManaComponent();
            if (manaComponent != null)
            {
                foreach (var cell in map.AllCells)
                {
                    var distanceSqr = cell.DistanceToSquared(everflower.Position);
                    if (distanceSqr < RadiusSqr)
                    {
                        var mana = baseMana * Mathf.Pow(1f - distanceSqr / RadiusSqr, 1.5f);
                        manaComponent.ChangeEnvironmentMana(cell, mana);
                    }
                }
            }

            var pawn = jobRitual.PawnWithRole("resonator");
            GenExplosion.DoExplosion(
                everflower.Position,
                everflower.Map,
                stunDamage,
                DamageDefOf.Stun,
                pawn,
                propagationSpeed: 0.5f,
                doSoundEffects: false);

            if (Rand.Chance(0.2f))
            {
                var incidentParms = new IncidentParms();
                incidentParms.target = map;
                incidentParms.points = StorytellerUtility.DefaultThreatPointsNow(everflower.Map) * Mathf.Max(0.5f, incidentScale);
                incidentParms.forced = true;

                if (VVIncidentDefOf.VV_TitanicHornetAssault.Worker.CanFireNow(incidentParms))
                {
                    Find.Storyteller.incidentQueue.Add(VVIncidentDefOf.VV_TitanicHornetAssault, GenTicks.TicksGame + Rand.Range(90000, 600000), incidentParms);
                }
            }

            if (Rand.Chance(0.1f))
            {
                var factionMechanoid = Find.FactionManager.OfMechanoids;
                if (!factionMechanoid.defeated && !factionMechanoid.deactivated)
                {
                    var incidentParms = new IncidentParms();
                    incidentParms.target = map;
                    incidentParms.points = StorytellerUtility.DefaultThreatPointsNow(everflower.Map) * Mathf.Max(0.5f, incidentScale);
                    incidentParms.faction = factionMechanoid;
                    incidentParms.forced = true;

                    if (IncidentDefOf.RaidEnemy.Worker.CanFireNow(incidentParms))
                    {
                        Find.Storyteller.incidentQueue.Add(IncidentDefOf.RaidEnemy, GenTicks.TicksGame + Rand.Range(90000, 600000), incidentParms);
                    }
                }
            }

            def.effecter.Spawn(everflower, map).Cleanup();
            everflower.Notify_RitualComplete(quality);
        }
    }
}
