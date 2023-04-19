using RimWorld;
using System.Collections.Generic;

namespace VVRace
{
    public class ApparelPropertiesExt : ApparelProperties
    {
        public List<BodyTypeDef> bodyTypeWhitelist;

        public BodyTypeDef fixedBodyType;

        public List<ThoughtDef> nullifyingThoughts;
    }
}
