using Verse;

namespace VVRace
{
    public abstract class SensorWorker
    {
        public abstract string TargetLabel { get; }

        public abstract bool Detected(Thing thing, float radius);
    }
}
