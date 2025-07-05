using Verse;

namespace VVRace
{
    public class PeaShooterGun : ThingWithComps
    {
        public ArcanePlant_Turret Turret
        {
            get
            {
                if (_turret == null)
                {
                    var compEquippable = GetComp<CompEquippable>();
                    _turret = compEquippable.PrimaryVerb.Caster as ArcanePlant_Turret;
                }

                return _turret;
            }
        }
        private ArcanePlant_Turret _turret;

        public override int? OverrideGraphicIndex => Turret.HasAnyBulletOverrides ? 1 : 0;
    }
}
