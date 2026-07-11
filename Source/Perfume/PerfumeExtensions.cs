using RimWorld;
using System.Collections.Generic;
using System.Linq;
using Verse;

namespace VVRace
{
    public class FlowerPerfumeExtension : DefModExtension
    {
    }

    public class ArcaneFlowerPerfumeExtension : DefModExtension
    {
        public string effectDescriptionKey;
        public ThingDef colorSource;
        public List<PerfumeName> names = new List<PerfumeName>();
        public List<PerfumeEffect> effects = new List<PerfumeEffect>();

        public string GetName(float weight)
        {
            var name = names
                .Where(candidate => candidate.minWeight <= weight)
                .OrderByDescending(candidate => candidate.minWeight)
                .FirstOrDefault();

            return name?.labelKey.NullOrEmpty() == false
                ? name.labelKey.Translate().Resolve()
                : null;
        }
    }

    public class PerfumeName
    {
        public float minWeight;
        public string labelKey;
    }

    public class PerfumeEffect
    {
        public float minWeight;
        public List<StatModifier> statOffsets;
        public List<StatModifier> statFactors;
    }
}
