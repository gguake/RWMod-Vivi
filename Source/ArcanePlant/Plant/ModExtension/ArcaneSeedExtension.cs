using UnityEngine;
using Verse;

namespace VVRace
{
    public class ArcaneSeedExtension : DefModExtension
    {
        public float seedMarketValue;
        public float seedMarketValueRatio;
        public Color seedColor;

        public SimpleCurve leavingSeedCountCurve;

        [Unsaved]
        public ThingDef seedDef;
    }
}
