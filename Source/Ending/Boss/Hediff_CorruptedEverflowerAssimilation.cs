using Verse;

namespace VVRace
{
    // 보스전에서 빚어진 거대 말벌이 지닌다. 죽으면 시체를 남기지 않고 아스포델 처치 때처럼 잿가루로 흩어진다.
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
