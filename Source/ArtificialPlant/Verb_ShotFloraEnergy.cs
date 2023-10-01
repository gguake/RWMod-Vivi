using Verse;

namespace VVRace
{
    public class Verb_ShotFloraEnergy : Verb_Shoot
    {
        protected override bool TryCastShot()
        {
            var shot =  base.TryCastShot();
            if (shot && caster is ArtificialPlant plant)
            {
                plant.Notify_TurretVerbShot();
            }

            return shot;
        }
    }
}
