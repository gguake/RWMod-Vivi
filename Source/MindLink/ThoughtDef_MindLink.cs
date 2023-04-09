using RimWorld;
using System.Collections.Generic;
using Verse;

namespace VVRace
{
    public class MindLinkStage
    {
        public int mindLinkElapsedTicks;
        public float moodMultiplier;
    }

    public class ThoughtDef_MindLink : ThoughtDef
    {
        public List<MindLinkStage> mindLinkStages;

        public override void ResolveReferences()
        {
            base.ResolveReferences();

            if (!mindLinkStages.NullOrEmpty())
            {
                mindLinkStages.SortBy(v => -v.mindLinkElapsedTicks);
            }
        }
    }
}
