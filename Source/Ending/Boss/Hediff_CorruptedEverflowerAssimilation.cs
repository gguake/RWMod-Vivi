using Verse;

namespace VVRace
{
    public class Hediff_CorruptedEverflowerAssimilation : HediffWithComps
    {
        public override void Notify_PawnDied(DamageInfo? dinfo, Hediff culprit = null)
        {
            base.Notify_PawnDied(dinfo, culprit);

            var corpse = pawn?.Corpse;
            if (corpse == null || !corpse.Spawned) { return; }

            var dissolving = (DissolvingHornet)ThingMaker.MakeThing(VVThingDefOf.VV_DissolvingHornet);
            dissolving.InitAshen(pawn, corpse);
            GenSpawn.Spawn(dissolving, corpse.Position, corpse.Map);
        }
    }
}
