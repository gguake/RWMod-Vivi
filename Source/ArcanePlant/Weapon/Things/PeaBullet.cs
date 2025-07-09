using HarmonyLib;
using RimWorld;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
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

        protected override void ImpactSomething()
        {
            //if (_overrideData != null)
            //{
            //    foreach (var data in _overrideData)
            //    {
            //        if (data.damageDef == DamageDefOf.Flame)
            //        {
            //            FleckMaker.Static(DrawPos, Map, VVFleckDefOf.HeatGlow_Intense, 0.75f);
            //        }
            //        else if (data.damageDef == DamageDefOf.Stun)
            //        {
            //            FleckMaker.Static(DrawPos, Map, VVFleckDefOf.ElectricalSpark, 0.75f);
            //        }
            //        else if (data.damageDef == DamageDefOf.Burn)
            //        {
            //            FleckMaker.Static(DrawPos, Map, VVFleckDefOf.Fleck_VaporizeCenterFlash, 0.25f);
            //        }
            //    }
            //}

            base.ImpactSomething();
        }

        protected override void Impact(Thing hitThing, bool blockedByShield = false)
        {
            PeaBulletReversePatch.Impact(this, hitThing, blockedByShield);
        }

        public static void Impact_TakeDamageOverride(DamageWorker.DamageResult damageResult, BattleLogEntry_RangedImpact log, PeaBullet bullet)
        {
            //hitThing.TakeDamage(dinfo);
            //var hitThing = damageResult?.hitThing;
            //var lastHitPart = damageResult?.LastHitPart;

            //hitThing.TakeDamage(new DamageInfo(
            //    .damageDef,
            //    data.amount,
            //    data.armorPenetration,
            //    bullet.ExactRotation.eulerAngles.y,
            //    bullet.launcher,
            //    lastHitPart,
            //    bullet.equipmentDef,
            //    DamageInfo.SourceCategory.ThingOrUnknown,
            //    bullet.intendedTarget.Thing,
            //    instigatorGuilty: false))?.AssociateWithLog(log);


            //if (hitThing != null && overrideData != null)
            //{
            //    foreach (var data in overrideData)
            //    {
            //        if (data.chance >= 1f || Rand.Chance(data.chance))
            //        {
            //            hitThing.TakeDamage(new DamageInfo(
            //                data.damageDef,
            //                data.amount,
            //                data.armorPenetration,
            //                bullet.ExactRotation.eulerAngles.y,
            //                bullet.launcher,
            //                lastHitPart,
            //                bullet.equipmentDef,
            //                DamageInfo.SourceCategory.ThingOrUnknown,
            //                bullet.intendedTarget.Thing,
            //                instigatorGuilty: false))?.AssociateWithLog(log);
            //        }
            //    }
            //}
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
                    new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(PeaBullet), nameof(PeaBullet.Impact_TakeDamageOverride))),
                });

                return instructions;
            }

            _ = Transpiler(null);
        }
    }
}
