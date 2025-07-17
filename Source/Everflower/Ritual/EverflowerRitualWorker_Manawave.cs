using Verse;

namespace VVRace
{
    public class EverflowerRitualWorker_Manawave : EverflowerRitualWorker_SimpleJob
    {
        private const float RadiusSqr = 50 * 50f;
        private const float Mana = 800f;

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
                        var mana = Mana * (distanceSqr / RadiusSqr);
                        manaComponent.ChangeEnvironmentMana(cell, mana);
                    }
                }
            }

            if (reservation.ritualDef.effectOnComplete != null)
            {
                reservation.ritualDef.effectOnComplete.SpawnMaintained(reservation.flower.Position, reservation.flower.Map);
            }

            GenExplosion.DoExplosion(
                reservation.flower.Position, 
                reservation.flower.Map, 
                50f, 
                VVDamageDefOf.VV_Manawave, 
                reservation.casterPawn,
                propagationSpeed: 0.5f);

            base.Complete(reservation);
        }
    }
}
