using RimWorld;
using UnityEngine;
using Verse;

namespace VVRace
{
    // 아포크리파가 결속된 요정 비비를 사출하는 포격탄. 착탄하면 금빛 폭발로 주변에 피해를 주고,
    // 보스·호위 말벌·보스가 피운 꽃은 피해에서 제외한다.
    // 혜성 꼬리(def 그림) 앞머리에 요정 비비(확장의 headGraphics)를 겹쳐 그려, 요정 비비가 창을 앞세우고 날아가는 모양이 된다.
    public class Projectile_FairyShell : Projectile_ApocryphaGimmick
    {
        // VFX/VV_FairyComet은 머리가 오른쪽(+U)을 향하므로 진행 방향으로 90도 돌려 그린다.
        public override Quaternion ExactRotation => base.ExactRotation * Quaternion.AngleAxis(-90f, Vector3.up);

        // 겹쳐 그리는 그림끼리의 높이 차이(같은 셰이더 안에서 뒤의 그림이 위에 오게)
        private const float HeadGraphicAltitudeStep = 0.002f;

        protected override void DrawAt(Vector3 drawLoc, bool flip = false)
        {
            base.DrawAt(drawLoc, flip);

            var headGraphics = Extension?.headGraphics;
            if (headGraphics.NullOrEmpty()) { return; }

            // 오른쪽을 향한 그림을 진행 방향으로 돌린다. 왼쪽으로 날아가면 좌우를 뒤집어 왼쪽을 향하게 한 뒤 돌린다(거꾸로 뒤집혀 보이지 않게).
            var direction = FlightDirection;
            var facingLeft = direction.x < 0f;
            var rotation = Quaternion.LookRotation(direction) * Quaternion.AngleAxis(facingLeft ? 90f : -90f, Vector3.up);

            var position = ExactPosition + Vector3.forward * CurrentArcHeight;
            position.y = drawLoc.y;
            for (int i = 0; i < headGraphics.Count; i++)
            {
                var data = headGraphics[i];
                var mesh = facingLeft ? MeshPool.GridPlaneFlip(data.drawSize) : MeshPool.GridPlane(data.drawSize);
                position.y += HeadGraphicAltitudeStep;
                Graphics.DrawMesh(mesh, position, rotation, data.Graphic.MatSingle, 0);
            }
        }

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
