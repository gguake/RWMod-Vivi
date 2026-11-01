using RimWorld;
using UnityEngine;
using Verse;

namespace VVRace
{
    public static class ResonanceWaveUtility
    {
        public const int StunTicks = 1200;

        private const float BeamWidth = 1.6f;

        public static void Trigger(Pawn attuner, ArcanePlant_Everflower everflower)
        {
            var map = everflower.Map;
            if (map == null) { return; }

            var budOffset = everflower.EverflowerComp.RitualEffecterOffset;
            EndingRitualDefOf.VV_Effecter_ResonanceWave.Spawn(everflower, map, budOffset).Cleanup();

            var quest = ViviEndingQuest.Current;
            var boss = quest?.Boss;
            var bossComp = quest?.BossComp;
            if (boss == null || bossComp == null) { return; }

            if (boss.Spawned && boss.Map == map)
            {
                var bossPos = bossComp.BodyCenter;
                FleckMaker.ConnectingLine(everflower.DrawPos + budOffset, bossPos, EndingRitualDefOf.VV_Fleck_ResonanceBeam, map, BeamWidth);
                EndingRitualDefOf.VV_Effecter_ResonanceWaveImpact.Spawn(boss, map, bossPos - boss.Position.ToVector3Shifted()).Cleanup();
            }

            bossComp.ApplyResonanceWave((Thing)attuner ?? everflower, StunTicks);

            Messages.Message(
                LocalizeString_EndingRituals.VV_Message_ResonanceWaveHit.Translate(
                    bossComp.Props.resonanceCooldownTicks.ToStringTicksToPeriod().Named("COOLDOWN")),
                boss,
                MessageTypeDefOf.PositiveEvent);
        }
    }
}
