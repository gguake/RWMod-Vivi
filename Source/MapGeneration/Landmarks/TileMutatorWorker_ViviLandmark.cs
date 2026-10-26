using RimWorld;
using RimWorld.Planet;
using Verse;

namespace VVRace
{
    // 비비 랜드마크 공통: 비비아 꽃이 자라는 타일에만 생기고, 맵 생성은 GenStep_ViviLandmark에서 한다.
    public abstract class TileMutatorWorker_ViviLandmark : TileMutatorWorker
    {
        protected TileMutatorWorker_ViviLandmark(TileMutatorDef def) : base(def)
        {
        }

        public override bool IsValidTile(PlanetTile tile, PlanetLayer layer)
        {
            return ViviLandmarkUtility.IsViviaGrowableTile(tile, layer, def.GetModExtension<ViviLandmarkExtension>());
        }

        public abstract void GenerateLandmark(Map map);
    }
}
