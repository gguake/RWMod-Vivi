using RimWorld;
using UnityEngine;
using Verse;

namespace VVRace
{
    // 아포크리파 2페이즈의 조율 의식 완료 효과: 제단에서 뿜어져 나온 공명 파동이 아포크리파를 강타한다.
    public static class ResonanceWaveUtility
    {
        // 공명 파동 기절 시간(20초)
        public const int StunTicks = 1200;

        private const float BeamWidth = 1.6f;

        public static void Trigger(Pawn attuner, ArcanePlant_Everflower everflower)
        {
            var map = everflower.Map;
            if (map == null) { return; }

            // 영원꽃 꽃봉오리에서 황금 파동이 크게 퍼져 나간다.
            var budOffset = everflower.EverflowerComp.RitualEffecterOffset;
            EndingRitualDefOf.VV_Effecter_ResonanceWave.Spawn(everflower, map, budOffset).Cleanup();

            var quest = ViviEndingQuest.Current;
            var boss = quest?.Boss;
            var bossComp = quest?.BossComp;
            if (boss == null || bossComp == null) { return; }

            if (boss.Spawned && boss.Map == map)
            {
                // 꽃봉오리 → 보스 몸통(연결선·조준선과 같은 기준점)으로 이어지는 빛줄기와 보스 위치의 마나 섬광
                var bossPos = bossComp.BodyCenter;
                FleckMaker.ConnectingLine(everflower.DrawPos + budOffset, bossPos, EndingRitualDefOf.VV_Fleck_ResonanceBeam, map, BeamWidth);
                EndingRitualDefOf.VV_Effecter_ResonanceWaveImpact.Spawn(boss, map, bossPos - boss.Position.ToVector3Shifted()).Cleanup();
            }

            // 기절·피해 감소 완화 중첩·채널링 중단은 보스 컴프가 처리한다.
            bossComp.ApplyResonanceWave((Thing)attuner ?? everflower, StunTicks);

            Messages.Message(
                LocalizeString_EndingRituals.VV_Message_ResonanceWaveHit.Translate(
                    bossComp.ResonanceStacks.Named("STACKS"),
                    bossComp.DamageReduction.ToStringPercent().Named("REDUCTION")),
                boss,
                MessageTypeDefOf.PositiveEvent);
        }
    }
}
