using RimWorld;
using UnityEngine;
using Verse;

namespace VVRace
{
    public class Projectile_FairyShell : Projectile_ApocryphaGimmick
    {
        public override Quaternion ExactRotation => base.ExactRotation * Quaternion.AngleAxis(-90f, Vector3.up);

        private const float HeadGraphicAltitudeStep = 0.002f;

        protected override void DrawAt(Vector3 drawLoc, bool flip = false)
        {
            base.DrawAt(drawLoc, flip);

            var headGraphics = Extension?.headGraphics;
            if (headGraphics.NullOrEmpty()) { return; }

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
