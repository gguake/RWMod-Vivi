using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace VVRace
{
    public class GeneBodyTypeOverride
    {
        public string overrideGraphicPath;

        public bool useUniqueHeadOffset = false;
        public bool applyLifeStageHeadOffset = true;
        public Vector2 headOffset;
    }

    public class GeneBodyTypeOverrideSet
    {
        public GeneBodyTypeOverride Child;
        public GeneBodyTypeOverride Thin;
        public GeneBodyTypeOverride Female;
    }

    public class GeneDefExt : GeneDef
    {
        public GeneBodyTypeOverrideSet bodyTypeOverrides;

        public float eggLayIntervalDays = -1f;
    }
}
