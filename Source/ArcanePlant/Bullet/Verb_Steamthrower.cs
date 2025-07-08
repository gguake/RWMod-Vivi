using RimWorld;
using Verse;

namespace VVRace
{
    public class Verb_Steamthrower : Verb_ShootBeam
    {
        public const float BarrelOffset = 5f;

        private CompMana _manaComp;
        public CompMana ManaComp
        {
            get
            {
                if (_manaComp == null)
                {
                    _manaComp = caster.TryGetComp<CompMana>();
                }

                if (_manaComp == null)
                {
                    _manaComp = EquipmentSource?.TryGetComp<CompMana>();
                }

                return _manaComp;
            }
        }

        public bool HasSufficientMana
        {
            get
            {
                if (Bursting)
                {
                    return ManaComp.Stored >= EquipmentSource?.GetStatValue(VVStatDefOf.VV_RangedWeapon_ManaCost);
                }
                else
                {
                    return ManaComp.Stored >= EquipmentSource?.GetStatValue(VVStatDefOf.VV_RangedWeapon_ManaCost) * BurstShotCount;
                }
            }
        }

        public override string ReportLabel => base.ReportLabel;

        public override bool ValidateTarget(LocalTargetInfo target, bool showMessages = true)
        {
            if (!base.ValidateTarget(target, showMessages))
            {
                return false;
            }

            if (!HasSufficientMana)
            {
                var manaCost = (int)(EquipmentSource?.GetStatValue(VVStatDefOf.VV_RangedWeapon_ManaCost) ?? 0);
                Messages.Message($"ManaNotEnough".Translate(manaCost.Named("MANACOST")), new LookTargets(caster), MessageTypeDefOf.RejectInput, historical: false);
            }

            return true;
        }

        public override bool Available()
        {
            if (!base.Available()) { return false; }

            return HasSufficientMana;
        }

        protected override bool TryCastShot()
        {
            var compMana = ManaComp;
            if (compMana == null) { return false; }

            var manaPerShoot = EquipmentSource?.GetStatValue(VVStatDefOf.VV_RangedWeapon_ManaCost) ?? 0;
            if (compMana.Stored < manaPerShoot) { return false; }

            var shot = base.TryCastShot();
            if (shot)
            {
                compMana.Stored -= manaPerShoot;
            }

            return shot;
        }
    }
}
