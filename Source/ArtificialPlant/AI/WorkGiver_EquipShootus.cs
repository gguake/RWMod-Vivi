using RimWorld;
using Verse;
using Verse.AI;

namespace VVRace
{
    public class WorkGiver_EquipShootus : WorkGiver_Scanner
    {
        public override ThingRequest PotentialWorkThingRequest => ThingRequest.ForDef(VVThingDefOf.VV_Shootus);

        public override PathEndMode PathEndMode => PathEndMode.ClosestTouch;

        public override bool HasJobOnThing(Pawn pawn, Thing t, bool forced = false)
        {
            var shootus = t as Shootus;
            if (shootus == null || !shootus.Spawned || shootus.ReservedWeapon == null || !shootus.ReservedWeapon.Spawned)
            {
                return false;
            }

            if (pawn.Faction != shootus.Faction)
            {
                return false;
            }

            if (!pawn.CanReserve(shootus, ignoreOtherReservations: forced))
            {
                return false;
            }

            if (!pawn.CanReserve(shootus.ReservedWeapon, ignoreOtherReservations: forced))
            {
                return false;
            }

            return true;
        }

        public override Job JobOnThing(Pawn pawn, Thing t, bool forced = false)
        {
            var shootus = t as Shootus;
            if (shootus == null || !shootus.Spawned || shootus.ReservedWeapon == null || !shootus.ReservedWeapon.Spawned) { return null; }

            if (pawn.Faction != shootus.Faction)
            {
                return null;
            }

            var weapon = shootus.ReservedWeapon;
            if (weapon == null) { return null; }

            if (!pawn.CanReach(shootus, PathEndMode.Touch, MaxPathDanger(pawn)))
            {
                return null;
            }

            if (!pawn.CanReach(weapon, PathEndMode.ClosestTouch, MaxPathDanger(pawn)))
            {
                return null;
            }

            var job = JobMaker.MakeJob(VVJobDefOf.VV_EquipWeaponToShootus, shootus, weapon);
            job.count = 1;

            return job;
        }
    }
}
