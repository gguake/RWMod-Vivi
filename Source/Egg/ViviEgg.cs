using Verse;

namespace VVRace
{
    public class ViviEgg : ThingWithComps
    {
        public override bool CanStackWith(Thing other)
        {
            return false;
        }

        public override bool TryAbsorbStack(Thing other, bool respectStackLimit)
        {
            return false;
        }
    }
}
