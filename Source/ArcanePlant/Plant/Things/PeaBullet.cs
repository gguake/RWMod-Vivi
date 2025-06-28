using HarmonyLib;
using RimWorld;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using Verse;

namespace VVRace
{
    public class PeaBullet : Bullet
    {
        private static HashSet<ThingDef> _tmpBulletOverridePlants = new HashSet<ThingDef>();

        public override void ExposeData()
        {
            base.ExposeData();
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
