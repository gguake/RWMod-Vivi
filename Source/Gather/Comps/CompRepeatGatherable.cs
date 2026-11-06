using RimWorld;
using System.Collections.Generic;
using Verse;

namespace VVRace
{
    public class CompProperties_RepeatGatherable : CompProperties
    {
        public CompProperties_RepeatGatherable()
        {
            compClass = typeof(CompRepeatGatherable);
        }
    }

    public class CompRepeatGatherable : ThingComp
    {
        public CompProperties_RepeatGatherable Props => (CompProperties_RepeatGatherable)props;

        public Dictionary<StatDef, int> lastGatheredTicks;

        public override void PostExposeData()
        {
            base.PostExposeData();

            Scribe_Collections.Look(ref lastGatheredTicks, "lastGatheredTicks", LookMode.Def, LookMode.Value);
        }

        public override void PostSpawnSetup(bool respawningAfterLoad)
        {
            parent.Map.GetComponent<GatheringMapComponent>().Notify_GatherableSpawned(parent);
        }

        public override void PostDeSpawn(Map map, DestroyMode mode = DestroyMode.Vanish)
        {
            map.GetComponent<GatheringMapComponent>().Notify_GatherableDespawned(parent);
        }

        public void Gathered(StatDef cooldownStat)
        {
            if (lastGatheredTicks == null) { lastGatheredTicks = new Dictionary<StatDef, int>(); }
            if (lastGatheredTicks.ContainsKey(cooldownStat))
            {
                lastGatheredTicks[cooldownStat] = Find.TickManager.TicksGame;
            }
            else
            {
                lastGatheredTicks.Add(cooldownStat, Find.TickManager.TicksGame);
            }
        }

        public bool IsCooldown(StatDef cooldownStat) => CooldownEndTick(cooldownStat) > Find.TickManager.TicksGame;

        // 재채집 대기가 끝나는 틱(대기 중이면 지금보다 크다). 채집한 적이 없으면 -1.
        public int CooldownEndTick(StatDef cooldownStat)
        {
            if (lastGatheredTicks == null || !lastGatheredTicks.TryGetValue(cooldownStat, out var ticks) || ticks <= 0) { return -1; }

            return ticks + (int)(parent.GetStatValue(cooldownStat, cacheStaleAfterTicks: 2000) * 60000f);
        }

        public override string CompInspectStringExtra()
        {
            if (lastGatheredTicks != null)
            {
                foreach (var kv in lastGatheredTicks)
                {
                    if (IsCooldown(kv.Key))
                    {
                        var cooldownTicks = kv.Value + (int)(parent.GetStatValue(kv.Key) * 60000f) - Find.TickManager.TicksGame;
                        return ("VV_Inspector_Cooldown_" + kv.Key.defName.Replace("VV_", "")).Translate(cooldownTicks.ToStringTicksToPeriod());
                    }
                }

            }

            return string.Empty;
        }
    }
}
