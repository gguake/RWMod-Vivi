using Verse;

namespace VVRace
{
    public class Verb_SpawnNeedle : Verb
    {
        public override void WarmupComplete()
        {
            base.WarmupComplete();
            Find.BattleLog.Add(new BattleLogEntry_RangedFire(
                caster, 
                currentTarget.HasThing ? currentTarget.Thing : null, 
                base.EquipmentSource?.def, 
                verbProps.defaultProjectile, 
                ShotsPerBurst > 1));
        }

        protected override bool TryCastShot()
        {
            if (currentTarget.HasThing && currentTarget.Thing.Map != caster.Map)
            {
                return false;
            }

            var projectileDef = verbProps.defaultProjectile;
            var projectileProps = projectileDef?.projectile as NeedleProperties;
            if (projectileProps == null)
            {
                return false;
            }

            if (!TryFindShootLineFromTo(caster.Position, currentTarget, out var resultingLine))
            {
                return false;
            }

            lastShotTick = Find.TickManager.TicksGame;

            var needle = GenSpawn.Spawn(projectileDef, resultingLine.Source, caster.Map) as Needle;
            needle.Launch(caster, EquipmentSource, currentTarget);

            return true;
        }

        public override float HighlightFieldRadiusAroundTarget(out bool needLOSToCenter)
        {
            needLOSToCenter = true;

            var projectile = verbProps.defaultProjectile?.projectile as NeedleProperties;
            if (projectile == null)
            {
                return 0f;
            }

            return projectile.targettingRadius;
        }

        public override bool Available()
        {
            if (!base.Available())
            {
                return false;
            }

            var projectile = verbProps.defaultProjectile?.projectile as NeedleProperties;
            return projectile != null;
        }
    }
}
