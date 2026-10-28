using RimWorld;
using Verse;

namespace VVRace
{
    // 멸망꽃/요정꽃 씨앗. 착탄한 칸(막혔으면 근처 빈 바닥)에 projectile.spawnsThingDef의 꽃을 피운다.
    // 방어막에 막혔거나, 날아가는 사이 보스가 결속에서 풀렸거나(2페이즈·퇴장) 엔딩이 보스 단계를 벗어났으면 꽃 없이 흩어진다.
    public class Projectile_ApocryphaSeed : Projectile_ApocryphaGimmick
    {
        protected override void Impact(Thing hitThing, bool blockedByShield = false)
        {
            // base.Impact에서 투사체를 Destroy하므로 필요한 값은 미리 보관한다.
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
