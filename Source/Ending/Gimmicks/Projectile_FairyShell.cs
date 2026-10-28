using RimWorld;
using UnityEngine;
using Verse;

namespace VVRace
{
    // 아포크리파가 결속된 요정 비비를 사출하는 포격탄. 착탄하면 금빛 폭발로 주변에 피해를 주고,
    // 보스·호위 말벌·보스가 피운 꽃은 피해에서 제외한다.
    public class Projectile_FairyShell : Projectile_ApocryphaGimmick
    {
        // VFX/VV_FairyComet은 머리가 오른쪽(+U)을 향하므로 진행 방향으로 90도 돌려 그린다.
        public override Quaternion ExactRotation => base.ExactRotation * Quaternion.AngleAxis(-90f, Vector3.up);

        protected override void Impact(Thing hitThing, bool blockedByShield = false)
        {
            // base.Impact에서 투사체를 Destroy하므로 필요한 값은 미리 보관한다.
            var map = Map;
            var cell = Position;
            var instigator = launcher;
            var damageDef = DamageDef;
            var damageAmount = DamageAmount;
            var armorPenetration = ArmorPenetration;
            var radius = def.projectile.explosionRadius;
            var intended = intendedTarget.Thing;
            var impactEffecter = Extension?.impactEffecter;

            base.Impact(hitThing, blockedByShield);

            if (map == null || !cell.InBounds(map)) { return; }

            ApocryphaGimmicks.PlayEffect(impactEffecter, cell, map);

            GenExplosion.DoExplosion(
                cell,
                map,
                radius,
                damageDef,
                instigator,
                damageAmount,
                armorPenetration,
                projectile: def,
                intendedTarget: intended,
                ignoredThings: ApocryphaGimmicks.GetBlastIgnoredThings(map, instigator),
                screenShakeFactor: def.projectile.screenShakeFactor);
        }
    }
}
