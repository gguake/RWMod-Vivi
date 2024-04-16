using UnityEngine;
using Verse;

namespace VVRace
{
    public class MoteDreamumTowerAttached : MoteAttached, IConditionalGraphicProvider
    {
        public int GraphicIndex
        {
            get
            {
                if (link1.Target.Thing is IConditionalGraphicProvider conditionalProvider)
                {
                    return conditionalProvider.GraphicIndex;
                }

                return 0;
            }
        }
    }
}
