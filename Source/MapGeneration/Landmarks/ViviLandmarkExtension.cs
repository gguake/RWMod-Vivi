using System.Collections.Generic;
using Verse;

namespace VVRace
{
    // 비비 랜드마크(TileMutatorDef) 설정. 월드 생성 조건과 맵 배치, 비비아 꽃밭 모양을 XML에서 조정한다.
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

    public class ManaVeinExtension : ViviLandmarkExtension
    {
        public float manaPerDay = 10000f;
    }

    public class ArcanePlantHabitatExtension : ViviLandmarkExtension
    {
        public List<ThingDef> plantKinds = new List<ThingDef>();
        public int plantKindCount = 3;
        public IntRange plantsPerClusterRange = new IntRange(2, 4);
        public float clusterRadius = 3f;
        public float plantSpacing = 2f;
    }
}
