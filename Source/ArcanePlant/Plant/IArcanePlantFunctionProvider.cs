using System.Collections.Generic;

namespace VVRace
{
    // 마법 식물 정보 탭(ITab_ArcanePlant)에 표시되는 고유 기능 설명을 제공한다.
    // 고유 기능을 가진 ThingComp가 구현하며, 설명 문구는 각 comp의 Props 값으로 조립한다.
    public interface IArcanePlantFunctionProvider
    {
        IEnumerable<string> GetFunctionDescriptions();
    }
}
