using System.Collections.Generic;
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

    public class ApocryphaProjectileExtension : DefModExtension
    {
        public EffecterDef launchEffecter;
        public EffecterDef impactEffecter;

        public FleckDef trailFleck;
        public Color trailColor = Color.white;
        public int trailIntervalTicks = 2;
        public FloatRange trailScale = new FloatRange(0.3f, 0.5f);

        public float headOffset;                // 텍스처 중심에서 진행 방향으로 잰 머리까지의 거리(칸).

        public List<GraphicData> headGraphics;
    }
}
