using HarmonyLib;
using RimWorld;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using UnityEngine;
using Verse;

namespace VVRace
{
    public struct BulletOverrideData : IExposable
    {
        public float shouldApplyTicks;
        public DamageDef damageDef;
        public float amount;
        public float armorPenetration;
        public float chance;

        public void ExposeData()
        {
            Scribe_Values.Look(ref shouldApplyTicks, "shouldApplyTicks");
            Scribe_Defs.Look(ref damageDef, "damageDef");
            Scribe_Values.Look(ref amount, "amount");
            Scribe_Values.Look(ref armorPenetration, "armorPenetration");
            Scribe_Values.Look(ref chance, "chance");
        }
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

        protected override void ImpactSomething()
        {
            if (_overrideData != null)
            {
                foreach (var data in _overrideData)
                {
                    if (data.damageDef == DamageDefOf.Flame)
                    {
                        FleckMaker.Static(DrawPos, Map, VVFleckDefOf.HeatGlow_Intense, 0.75f);
                    }
                    else if (data.damageDef == DamageDefOf.Stun)
                    {
                        FleckMaker.Static(DrawPos, Map, VVFleckDefOf.ElectricalSpark, 0.75f);
                    }
                    else if (data.damageDef == DamageDefOf.Burn)
                    {
                        FleckMaker.Static(DrawPos, Map, VVFleckDefOf.Fleck_VaporizeCenterFlash, 0.25f);
                    }
                }
            }

            base.ImpactSomething();
        }

        protected override void Impact(Thing hitThing, bool blockedByShield = false)
        {
            PeaBulletReversePatch.Impact(this, hitThing, blockedByShield);
        }

        public static void ImpactPeaOverride(DamageWorker.DamageResult damageResult, BattleLogEntry_RangedImpact log, PeaBullet bullet)
        {
            var hitThing = damageResult?.hitThing;
            var lastHitPart = damageResult?.LastHitPart;
            var overrideData = bullet._overrideData;

            if (hitThing != null && overrideData != null)
            {
                foreach (var data in overrideData)
                {
                    if (data.chance >= 1f || Rand.Chance(data.chance))
                    {
                        hitThing.TakeDamage(new DamageInfo(
                            data.damageDef,
                            data.amount,
                            data.armorPenetration,
                            bullet.ExactRotation.eulerAngles.y,
                            bullet.launcher,
                            lastHitPart,
                            bullet.equipmentDef,
                            DamageInfo.SourceCategory.ThingOrUnknown,
                            bullet.intendedTarget.Thing,
                            instigatorGuilty: false))?.AssociateWithLog(log);
                    }
                }
            }
        }
    }

    [HarmonyPatch]
    public class PeaBulletReversePatch
    {
        [HarmonyReversePatch]
        [HarmonyPatch(typeof(Bullet), "Impact")]
        public static void Impact(Bullet __instance, Thing hitThing, bool blockedByShield)
        {
            IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> codeInstructions)
            {
                var instructions = codeInstructions.ToList();
                var index = instructions.FirstIndexOf(v => v.opcode == OpCodes.Callvirt && v.OperandIs(AccessTools.Method(typeof(Thing), nameof(Thing.TakeDamage)))) + 1;

                instructions.InsertRange(index, new CodeInstruction[]
                {
                    new CodeInstruction(OpCodes.Dup),
                    new CodeInstruction(OpCodes.Ldloc_2),
                    new CodeInstruction(OpCodes.Ldarg_0),
                    new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(PeaBullet), nameof(PeaBullet.ImpactPeaOverride))),
                });

                return instructions;
            }

            _ = Transpiler(null);
        }
    }
}
