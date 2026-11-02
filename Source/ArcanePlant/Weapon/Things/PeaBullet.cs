using RimWorld;
using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace VVRace
{
    public class PeaBullet : Bullet
    {
        private List<BulletModifier> _modifiers = new List<BulletModifier>();

        public override int DamageAmount
        {
            get
            {
                var damage = base.DamageAmount;
                foreach (var mod in _modifiers)
                {
                    damage += mod.additionalDamage;
                }

                return damage;
            }
        }

        public override float ArmorPenetration
        {
            get
            {
                var penetration = base.ArmorPenetration;
                foreach (var mod in _modifiers)
                {
                    penetration += mod.additionalArmorPenetration;
                }

                return penetration;
            }
        }

        public override void ExposeData()
        {
            base.ExposeData();

            Scribe_Collections.Look(ref _modifiers, "modifiers", LookMode.Deep);
        }

        public override void Launch(Thing launcher, Vector3 origin, LocalTargetInfo usedTarget, LocalTargetInfo intendedTarget, ProjectileHitFlags hitFlags, bool preventFriendlyFire = false, Thing equipment = null, ThingDef targetCoverDef = null)
        {
            base.Launch(launcher, origin, usedTarget, intendedTarget, hitFlags, preventFriendlyFire, equipment, targetCoverDef);

            if (launcher is ArcanePlant_Turret turret)
            {
                foreach (var mod in turret.BulletModifiers)
                {
                    _modifiers.Add(mod);
                }
            }
        }
    }
}
