using RimWorld;
using UnityEngine;
using Verse;

namespace VVRace
{
    public class GatherableFilthExtension : DefModExtension
    {
        public StatDef yieldStat;
        public ThingDef thingDef;
        public int amount;

        // 오물 하나를 털어 얻는 양(소수 부분은 쓰는 쪽이 무작위로 반올림한다): 정의의 양 × 털어 내는 폰의 채집량 스탯(0.1~2배), 최소 1
        public float YieldFor(Pawn gatherer) => Mathf.Max(1f, amount * Mathf.Clamp(gatherer.GetStatValue(yieldStat), 0.1f, 2.0f));
    }
}
