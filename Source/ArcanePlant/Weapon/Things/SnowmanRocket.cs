using RimWorld;
using Verse;

namespace VVRace
{
    public class SnowmanRocket : Projectile_Explosive
    {
        protected override void Explode()
        {
            var map = Map;
            var position = Position;

            base.Explode();

            if (GenSpawn.TrySpawn(VVThingDefOf.VV_ExplosiveSnowman, position, map, out var snowman, canWipeEdifices: false))
            {
                snowman.TryGetComp<CompExplosiveSnowman>()?.Arm(launcher, equipmentDef, def, intendedTarget.Thing);
            }
            else if (snowman != null)
            {
                snowman.Destroy();
            }

            foreach (var cell in GenRadial.RadialCellsAround(position, def.projectile.explosionRadius, true))
            {
                if (!cell.InBounds(map)) { continue; }
                map.snowGrid.AddDepth(cell, 1f);
            }
        }
    }
}
