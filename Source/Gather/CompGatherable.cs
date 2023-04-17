using RimWorld;
using Verse;

namespace VVRace
{
    public class CompProperties_Gatherable : CompProperties
    {
        public StatDef cooldownStat;

        public CompProperties_Gatherable()
        {
            compClass = typeof(CompGatherable);
        }
    }

    public class CompGatherable : ThingComp
    {
        public CompProperties_Gatherable Props => (CompProperties_Gatherable)props;

        public int lastGatheredTicks;

        public override void PostExposeData()
        {
            base.PostExposeData();

            Scribe_Values.Look(ref lastGatheredTicks, "lastGatheredTicks");
        }

        public void Gathered()
        {
            lastGatheredTicks = Find.TickManager.TicksGame;
        }

        public bool IsCooldown()
            => lastGatheredTicks > 0 && Find.TickManager.TicksGame < lastGatheredTicks + (int)(parent.GetStatValue(Props.cooldownStat) * 60000f);

        public override string CompInspectStringExtra()
        {
            if (IsCooldown())
            {
                var cooldownTicks = lastGatheredTicks + (int)(parent.GetStatValue(Props.cooldownStat) * 60000f) - Find.TickManager.TicksGame;
                return LocalizeTexts.InspectorGatherCooldown.Translate(cooldownTicks.ToStringTicksToPeriod());
            }

            return string.Empty;
        }
    }
}
