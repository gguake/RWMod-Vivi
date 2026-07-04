using Verse;

namespace VVRace
{
    public abstract class ManaFluxRule
    {
        public abstract IntRange FluxRangeForDisplay { get; }

        public abstract int CalcManaFlux(Thing thing);

        public abstract string GetRuleString();

        // 정보 탭 목록에 표시하는 간략한 조건 라벨. 상세 수치는 GetRuleString()이 담당한다.
        public virtual string GetRuleLabel() => GetRuleString();
    }

}
