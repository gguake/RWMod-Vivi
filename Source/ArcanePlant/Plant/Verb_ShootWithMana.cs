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

                return _compMana;
            }
        }

        protected override bool TryCastShot()
        {
            var compMana = CompMana;
            if (compMana == null) { return false; }

            var manaPerShoot = EquipmentSource.GetStatValue(VVStatDefOf.VV_RangedWeapon_ManaCost);
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
