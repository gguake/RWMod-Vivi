using RimWorld;
using Verse;

namespace VVRace
{
    // 비비 랜드마크의 맵 생성 단계. TileMutatorDef.extraGenSteps로 붙여서, UsedRects를 보지 않고 바위 무더기를 흩뿌리는
    // 바닐라 RockChunks(970) 뒤에 실행한다. 이미 자란 야생 풀은 꽃밭/군락 자리에서 걷어 낸다.
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
