using System.Collections.Generic;
using Verse;

namespace VVRace
{
    public class DamageWorker_ManawaveStun : DamageWorker_Stun
    {
        protected override void ExplosionDamageThing(
            Explosion explosion,
            Thing thing,
            List<Thing> damagedThings,
            List<Thing> ignoredThings,
            IntVec3 cell)
        {
            if (!(thing is Pawn)) { return; }

            base.ExplosionDamageThing(explosion, thing, damagedThings, ignoredThings, cell);
        }
    }
}
