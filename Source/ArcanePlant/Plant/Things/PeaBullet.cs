using RimWorld;
using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace VVRace
{
    public struct BulletOverrideData
    {
        public float shouldApplyTicks;
        public DamageDef damageDef;
        public float amount;
        public float armorPenetration;
        public float chance;
    }

    public class PeaBullet : Bullet
    {
        private static HashSet<ThingDef> _tmpBulletOverridePlants = new HashSet<ThingDef>();

        protected List<BulletOverrideData> _overrideData;

        public override void ExposeData()
        {
            base.ExposeData();

            Scribe_Collections.Look(ref _overrideData, "overrideData", LookMode.Deep);
        }

        public override void Launch(Thing launcher, Vector3 origin, LocalTargetInfo usedTarget, LocalTargetInfo intendedTarget, ProjectileHitFlags hitFlags, bool preventFriendlyFire = false, Thing equipment = null, ThingDef targetCoverDef = null)
        {
            base.Launch(launcher, origin, usedTarget, intendedTarget, hitFlags, preventFriendlyFire, equipment, targetCoverDef);

            var directionalVector = destination - origin;
            var magnitude = directionalVector.magnitude;

            var maxOrthogonal = Mathf.Max(directionalVector.x, directionalVector.z);
            for (int n = 1; n < maxOrthogonal; ++n)
            {
                var v = (origin + directionalVector * (n / magnitude)).ToIntVec3();

                var arcanePlant = v.GetFirstThing<ArcanePlant>(Map);
                if (arcanePlant == null || !arcanePlant.Spawned || arcanePlant.ManaChargeRatio < 0.01f || _tmpBulletOverridePlants.Contains(arcanePlant.def)) { continue; }

                var bulletOverrides = arcanePlant.ArcanePlantModExtension.passedBulletOverrides;
                if (bulletOverrides != null && bulletOverrides.Count > 0)
                {
                    foreach (var bulletOverride in bulletOverrides)
                    {
                        if (bulletOverride.thingDef != def) { continue; }

                        if (_overrideData == null)
                        {
                            _overrideData = new List<BulletOverrideData>();
                        }

                        _overrideData.Add(new BulletOverrideData()
                        {
                            shouldApplyTicks = (n / magnitude) / def.projectile.SpeedTilesPerTick,
                            damageDef = bulletOverride.damageDef,
                            amount = bulletOverride.amount,
                            armorPenetration = bulletOverride.armorPenetration,
                            chance = bulletOverride.chance,
                        });

                        _tmpBulletOverridePlants.Add(arcanePlant.def);
                    }
                }
            }

            _tmpBulletOverridePlants.Clear();
        }

        protected override void Impact(Thing hitThing, bool blockedByShield = false)
        {
            base.Impact(hitThing, blockedByShield);

            if (hitThing != null && _overrideData != null)
            {
                bool instigatorGuilty = !(launcher is Pawn pawn) || !pawn.Drafted;
                foreach (var data in _overrideData)
                {
                    if (data.chance >= 1f || Rand.Chance(data.chance))
                    {
                        hitThing.TakeDamage(new DamageInfo(
                            data.damageDef,
                            data.amount,
                            data.armorPenetration,
                            ExactRotation.eulerAngles.y,
                            launcher,
                            null,
                            equipmentDef,
                            DamageInfo.SourceCategory.ThingOrUnknown,
                            intendedTarget.Thing,
                            instigatorGuilty));
                    }
                }
            }
        }
    }
}
