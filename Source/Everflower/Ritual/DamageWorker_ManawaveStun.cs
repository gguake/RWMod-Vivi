using RimWorld;
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
            if (!(thing is Pawn pawn) || damagedThings.Contains(pawn)) { return; }

            damagedThings.Add(pawn);
            if (ignoredThings != null && ignoredThings.Contains(pawn)) { return; }
            if (!IsHostileTarget(explosion, pawn)) { return; }

            var angle = pawn.Position == explosion.Position ?
                Rand.RangeInclusive(0, 359) :
                (pawn.Position - explosion.Position).AngleFlat;

            var dinfo = new DamageInfo(
                DamageDefOf.Stun,
                explosion.GetDamageAmountAt(cell),
                explosion.GetArmorPenetrationAt(cell),
                angle,
                explosion.instigator,
                weapon: explosion.weapon,
                intendedTarget: explosion.intendedTarget);

            var logEntry = new BattleLogEntry_ExplosionImpact(explosion.instigator, pawn, explosion.weapon, explosion.projectile, DamageDefOf.Stun);
            Find.BattleLog.Add(logEntry);
            pawn.TakeDamage(dinfo).AssociateWithLog(logEntry);
        }

        private static bool IsHostileTarget(Explosion explosion, Pawn pawn)
        {
            if (explosion.instigator != null)
            {
                return pawn.HostileTo(explosion.instigator);
            }

            return Faction.OfPlayerSilentFail != null && pawn.HostileTo(Faction.OfPlayerSilentFail);
        }
    }
}
