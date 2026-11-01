using RimWorld;
using Verse;

namespace VVRace
{
    public class Projectile_ApocryphaSeed : Projectile_ApocryphaGimmick
    {
        protected override void Impact(Thing hitThing, bool blockedByShield = false)
        {
            // base.Impact가 투사체를 파괴하므로 이후에 쓸 값은 미리 보관한다.
            var map = Map;
            var cell = Position;
            var flowerDef = def.projectile.spawnsThingDef;
            var boss = launcher as Pawn;
            var fizzleEffecter = Extension?.impactEffecter;

            base.Impact(hitThing, blockedByShield);

            if (map == null || flowerDef == null || !cell.InBounds(map)) { return; }

            if (blockedByShield || !ApocryphaGimmicks.IsGimmickActive(boss) || !ApocryphaGimmicks.TryFindBloomCellNear(cell, map, out var bloomCell))
            {
                ApocryphaGimmicks.PlayEffect(fizzleEffecter, cell, map);
                return;
            }

            ApocryphaGimmicks.SpawnFlower(flowerDef, bloomCell, map, boss);
        }
    }
}
