using Verse;

namespace VVRace
{
    public class SnowmanRocket : Projectile
    {
        protected override void Impact(Thing hitThing, bool blockedByShield = false)
        {
            base.Impact(hitThing, blockedByShield);

        }

        protected override void ImpactSomething()
        {
            base.ImpactSomething();


        }
    }
}
