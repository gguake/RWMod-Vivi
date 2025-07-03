using RimWorld;
using System.Collections.Generic;
using System.Xml;
using Verse;

namespace VVRace
{
    public class CompProperties_EquippableManaWeapon : CompProperties
    {
        public SimpleCurve meleeDamageOffsetCurve;
        public SimpleCurve meleeDamageFactorCurve;

        public CompProperties_EquippableManaWeapon()
        {
            compClass = typeof(CompEquippableManaWeapon);
        }
    }

    public class CompEquippableManaWeapon : CompEquippable
    {
        public CompProperties_EquippableManaWeapon Props => (CompProperties_EquippableManaWeapon)props;

        public CompMana ManaComp
        {
            get
            {
                if (_manaComp == null) { _manaComp = parent.GetComp<CompMana>(); }
                return _manaComp;
            }
        }
        private CompMana _manaComp;

        public override float GetStatOffset(StatDef stat)
        {
            if (stat == StatDefOf.MeleeWeapon_DamageMultiplier)
            {
                if (Props.meleeDamageOffsetCurve != null)
                {
                    var manaComp = ManaComp;
                    if (manaComp != null)
                    {
                        return Props.meleeDamageOffsetCurve.Evaluate(ManaComp.StoredPct);
                    }
                }
            }

            return 0f;
        }

        public override float GetStatFactor(StatDef stat)
        {
            if (stat == StatDefOf.MeleeWeapon_DamageMultiplier)
            {
                if (Props.meleeDamageFactorCurve != null)
                {
                    var manaComp = ManaComp;
                    if (manaComp != null)
                    {
                        return Props.meleeDamageFactorCurve.Evaluate(ManaComp.StoredPct);
                    }
                }
            }

            return 1f;
        }

        public override IEnumerable<Gizmo> CompGetEquippedGizmosExtra()
        {
            if (Holder?.Faction?.IsPlayer ?? false)
            {
                yield return new ManaGizmo(ManaComp);
            }
        }

        public override void Notify_UsedWeapon(Pawn pawn)
        {
            if (parent.def.IsMeleeWeapon)
            {
                var cost = parent.GetStatValue(VVStatDefOf.VV_MeleeWeapon_ManaCost);
                if (cost > 0 && ManaComp.Stored >= cost)
                {
                    ManaComp.Stored -= cost;
                }
            }
        }
    }
}
