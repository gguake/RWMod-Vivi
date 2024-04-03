using Verse;

namespace VVRace
{
    public class Verb_ShootWithMana : Verb_Shoot
    {
        protected override bool TryCastShot()
        {
            var shot =  base.TryCastShot();
            if (shot && caster is ArcanePlant plant)
            {
                plant.Notify_TurretVerbShot();
            }

            return shot;
        }
    }
}
