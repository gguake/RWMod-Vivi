using RimWorld;
using RimWorld.Planet;
using Verse;

namespace VVRace
{
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
