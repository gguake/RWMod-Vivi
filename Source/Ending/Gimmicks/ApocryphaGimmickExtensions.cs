using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace VVRace
{
    // 멸망꽃/요정꽃 공용 연출 데이터. 보스와 잇는 영혼 연결선의 색·폭·꽃 머리 위치도 여기서 읽는다.
    // (꽃이 있는 동안의 유지 이펙트는 def의 CompProperties_Effecter로 지정한다)
    public class ApocryphaFlowerExtension : DefModExtension
    {
        public EffecterDef bloomEffecter;       // 씨앗이 착탄해 피어날 때(최초 스폰)
        public EffecterDef witherEffecter;      // 시들거나 사라질 때

        public Color linkColor = Color.white;
        public float linkWidth = 0.6f;
        public Vector3 linkOffset;              // DrawPos 기준 꽃 머리 위치
    }

    // 아포크리파가 쏘는 기믹 투사체(요정 비비 사출탄, 씨앗) 공용 연출 데이터.
    public class ApocryphaProjectileExtension : DefModExtension
    {
        public EffecterDef launchEffecter;      // 발사 시 보스 몸통 위치
        public EffecterDef impactEffecter;      // 사출탄: 착탄 / 씨앗: 꽃을 피우지 못했을 때

        public FleckDef trailFleck;             // 포물선을 따라 남기는 반짝임
        public Color trailColor = Color.white;
        public int trailIntervalTicks = 2;
        public FloatRange trailScale = new FloatRange(0.3f, 0.5f);

        public float headOffset;                // 텍스처의 머리가 중심에서 진행 방향으로 떨어진 거리(칸)

        // 실제 투사체 위치(포물선 높이 포함)에 차례로 겹쳐 그리는 그림(요정 비비 사출탄: 후광, 요정 비비).
        // 오른쪽을 향한 그림을 진행 방향으로 기울이고, 왼쪽으로 날아가면 좌우를 뒤집는다(Projectile_FairyShell).
        public List<GraphicData> headGraphics;
    }
}
