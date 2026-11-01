using RimWorld;
using Verse;

namespace VVRace
{
    // RockChunks(970)는 UsedRects를 무시하므로 TileMutatorDef.extraGenSteps로 그 뒤에 실행한다.
    public class GenStep_ViviLandmark : GenStep
    {
        public TileMutatorDef mutator;

        public override int SeedPart => 1530297431;

        public override void Generate(Map map, GenStepParams parms)
        {
            if (mutator?.Worker is TileMutatorWorker_ViviLandmark worker)
            {
                worker.GenerateLandmark(map);
            }
        }
    }
}
