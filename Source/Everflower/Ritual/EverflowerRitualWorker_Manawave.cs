using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace VVRace
{
    public class EverflowerRitualWorker_Manawave : EverflowerRitualWorker_SimpleJob
    {
        private const float RadiusSqr = 55f * 55f;
        private const float Mana = 200f;

        public EverflowerRitualWorker_Manawave(EverflowerRitualDef def) : base(def)
        {
        }

        public override void Complete(EverflowerRitualReservation reservation)
        {
            var manaComponent = reservation.flower.Map.GetManaComponent();
            if (manaComponent != null)
            {
                foreach (var cell in reservation.flower.Map.AllCells)
                {
                    var distanceSqr = cell.DistanceToSquared(reservation.flower.Position);
                    if (distanceSqr < RadiusSqr)
                    {
                        var mana = Mana * Mathf.Pow(1f - distanceSqr / RadiusSqr, 1.5f);
                        manaComponent.ChangeEnvironmentMana(cell, mana);
                    }
                }
            }

            GenExplosion.DoExplosion(
                reservation.flower.Position, 
                reservation.flower.Map, 
                55f, 
                DamageDefOf.Stun, 
                reservation.casterPawn,
                propagationSpeed: 0.5f,
                doSoundEffects: false);

            if (Rand.Chance(_def.incidentProb))
            {
                var incidentParms = new IncidentParms();
                incidentParms.target = reservation.flower.Map;
                incidentParms.points = StorytellerUtility.DefaultThreatPointsNow(reservation.flower.Map) * Rand.Range(1.5f, 3f);
                incidentParms.forced = true;

                if (VVIncidentDefOf.VV_TitanicHornetAssault.Worker.CanFireNow(incidentParms))
                {
                    Find.Storyteller.incidentQueue.Add(VVIncidentDefOf.VV_TitanicHornetAssault, GenTicks.TicksGame + _def.incidentDelayTicks.RandomInRange, incidentParms);
                }
            }

            base.Complete(reservation);
        }
    }
}
