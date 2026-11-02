using UnityEngine;
using Verse;

namespace VVRace
{
    public class ApocryphaFlowerExtension : DefModExtension
    {
        public EffecterDef bloomEffecter;
        public EffecterDef witherEffecter;

        public Color linkColor = Color.white;
        public float linkWidth = 0.6f;
        public Vector3 linkOffset;              // DrawPos 기준 꽃 머리 위치
    }
}
