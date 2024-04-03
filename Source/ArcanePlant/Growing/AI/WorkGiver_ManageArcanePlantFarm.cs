using RimWorld;
using Verse;
using Verse.AI;

namespace VVRace
{
    public class WorkGiver_ManageArcanePlantFarm : WorkGiver_Scanner
    {
        public override ThingRequest PotentialWorkThingRequest => ThingRequest.ForDef(VVThingDefOf.VV_ArcanePlantFarm);

        public override PathEndMode PathEndMode => PathEndMode.Touch;

        public override bool HasJobOnThing(Pawn pawn, Thing t, bool forced = false)
        {
            if (!(t is Building_ArcanePlantFarm arcanePlantFarm)) { return false; }

            if (t.IsForbidden(pawn) || !pawn.CanReserve(t, ignoreOtherReservations: forced) || t.IsBurning() || pawn.Faction != t.Faction)
            {
                return false;
            }

            if (pawn.Map.designationManager.DesignationOn(t, DesignationDefOf.Deconstruct) != null)
            {
                return false;
            }

            if (arcanePlantFarm.Bill == null || 
                arcanePlantFarm.Bill.Stage != GrowingArcanePlantBillStage.Growing ||
                !arcanePlantFarm.Bill.ManagementRequired) { return false; }

            return true;
        }

        public override Job JobOnThing(Pawn pawn, Thing t, bool forced = false)
        {
            if (!(t is Building_ArcanePlantFarm arcanePlantFarm)) { return null; }

            if (arcanePlantFarm.Bill == null ||
                arcanePlantFarm.Bill.Stage != GrowingArcanePlantBillStage.Growing ||
                !arcanePlantFarm.Bill.ManagementRequired) { return null; }

            return JobMaker.MakeJob(VVJobDefOf.VV_ManageArcanePlantFarm, t);
        }
    }
}
