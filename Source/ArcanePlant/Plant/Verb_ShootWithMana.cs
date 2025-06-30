using RimWorld;
using Verse;

namespace VVRace
{
    public class Verb_ShootWithMana : Verb_Shoot
    {
        private CompMana _compMana;
        public CompMana CompMana
        {
            get
            {
                if (_compMana == null)
                { 
                    _compMana = caster.TryGetComp<CompMana>();
                }

                if (_compMana == null)
                {
                    _compMana = EquipmentSource?.TryGetComp<CompMana>();
                }

                return _compMana;
            }
        }

        public bool HasSufficientMana
        {
            get
            {
                if (Bursting)
                {
                    return CompMana.Stored >= EquipmentSource?.GetStatValue(VVStatDefOf.VV_RangedWeapon_ManaCost);
                }
                else
                {
                    return CompMana.Stored >= EquipmentSource?.GetStatValue(VVStatDefOf.VV_RangedWeapon_ManaCost) * BurstShotCount;
                }
            }
        }

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

        public override string ReportLabel => base.ReportLabel;

        protected override bool TryCastShot()
        {
            var compMana = CompMana;
            if (compMana == null) { return false; }

            var manaPerShoot = EquipmentSource?.GetStatValue(VVStatDefOf.VV_RangedWeapon_ManaCost) ?? 0;
            if (compMana.Stored < manaPerShoot) { return false; }

            var shot =  base.TryCastShot();
            if (shot)
            {
                compMana.Stored -= manaPerShoot;
            }

            return shot;
        }
    }
}
