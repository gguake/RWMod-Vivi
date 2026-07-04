using Verse;

namespace VVRace
{
    public abstract class SensorWorker
    {
        // 정보 탭 등에서 감지 대상을 설명할 때 사용하는 라벨
        public abstract string TargetLabel { get; }

        public abstract bool Detected(Thing thing, float radius);
    }
}
