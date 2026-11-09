using RimWorld;
using UnityEngine;
using Verse;

namespace VVRace
{
    public class Projectile_FairyShell : Projectile_ApocryphaGimmick
    {
        // VFX/VV_FairyComet의 머리는 오른쪽(+U)이므로 진행 방향에 맞춰 90도 회전 보정한다.
        public override Quaternion ExactRotation => base.ExactRotation * Quaternion.AngleAxis(-90f, Vector3.up);

        // 같은 셰이더의 그림을 겹칠 때 뒤에 그린 그림이 위에 오도록 주는 높이 차이.
        private const float HeadGraphicAltitudeStep = 0.002f;

        protected override void DrawAt(Vector3 drawLoc, bool flip = false)
        {
            base.DrawAt(drawLoc, flip);

            var headGraphics = Extension?.headGraphics;
            if (headGraphics.NullOrEmpty()) { return; }

            // 왼쪽으로 날아갈 때는 먼저 좌우를 반전한 뒤 회전해 그림이 거꾸로 보이지 않게 한다.
            var direction = FlightDirection;
            var facingLeft = direction.x < 0f;
            var rotation = Quaternion.LookRotation(direction) * Quaternion.AngleAxis(facingLeft ? 90f : -90f, Vector3.up);

            var position = ArcDrawPos;
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
            // base.Impact가 투사체를 파괴하므로 이후에 쓸 값은 미리 보관한다.
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
