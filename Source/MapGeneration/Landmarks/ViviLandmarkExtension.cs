using System.Collections.Generic;
using Verse;

namespace VVRace
{
    public class ViviLandmarkExtension : DefModExtension
    {
        public float minPlantDensity = 0.1f;
        public int minGrowingTwelfths = 6;

        // 개수는 250x250 맵 기준이며 맵 넓이에 비례한다.
        public IntRange countRange = new IntRange(3, 5);
        public int minCount = 1;
        public float minSpacing = 25f;
        public int edgeMargin = 15;

        public FloatRange fieldRadiusRange = new FloatRange(4f, 5f);
        public float fieldDensity = 0.7f;
        public IntRange fieldColorCountRange = new IntRange(2, 3);
    }

    public class ArcanePlantHabitatExtension : ViviLandmarkExtension
    {
        public List<ThingDef> plantKinds = new List<ThingDef>();
        public int plantKindCount = 3;
        public IntRange plantsPerClusterRange = new IntRange(2, 4);
        public float clusterRadius = 3f;
        public float plantSpacing = 2f;

        // 군락 중심마다 두는 영맥 한 곳의 하루 마나 방출량.
        public float manaPerDay = 10000f;
    }
}
