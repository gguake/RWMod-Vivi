using Verse;

namespace VVRace
{
    public class ManaEmitter : Building
    {
        public override int UpdateRateTicks => 1200;
        protected override int MaxTickIntervalRate => 1000;
    }
}
