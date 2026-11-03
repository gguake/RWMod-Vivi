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
            var linkSeed = thingIDNumber;

            base.Impact(hitThing, blockedByShield);

            if (map == null || flowerDef == null || !cell.InBounds(map)) { return; }

            if (blockedByShield || !ApocryphaGimmicks.IsGimmickActive(boss) || !ApocryphaGimmicks.TryFindBloomCellNear(cell, map, out var bloomCell))
            {
                ApocryphaGimmicks.PlayEffect(fizzleEffecter, cell, map);
                return;
            }

            // 날아오는 동안 이어져 있던 연결선을 꽃이 같은 꽃잎 배치로 이어받는다.
            ApocryphaGimmicks.SpawnFlower(flowerDef, bloomCell, map, boss, linkSeed);
        }
    }
}
