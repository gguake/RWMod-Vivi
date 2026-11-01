using System.Collections.Generic;

namespace VVRace
{
    public interface IArcanePlantFunctionProvider
    {
        IEnumerable<string> GetFunctionDescriptions();
    }
}
