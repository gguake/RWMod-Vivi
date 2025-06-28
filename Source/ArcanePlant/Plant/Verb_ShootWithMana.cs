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
            var shot =  base.TryCastShot();
            if (shot && compMana != null)
            {
                // TODO
            }

            return shot;
        }
    }
}
