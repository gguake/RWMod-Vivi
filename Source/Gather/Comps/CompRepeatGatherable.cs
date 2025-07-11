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

        public bool IsCooldown(StatDef cooldownStat)
        {
            if (lastGatheredTicks == null) { return false; }
            if (lastGatheredTicks.TryGetValue(cooldownStat, out var ticks))
            {
                return ticks > 0 && Find.TickManager.TicksGame < ticks + (int)(parent.GetStatValue(cooldownStat) * 60000f);
            }
            else
            {
                return false;
            }
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
