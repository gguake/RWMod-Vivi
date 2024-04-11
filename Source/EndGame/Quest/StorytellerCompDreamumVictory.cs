using RimWorld;
using System.Collections.Generic;
using System.Linq;
using Verse;

namespace VVRace
{
    public class StorytellerCompProperties_DreamumVictory : StorytellerCompProperties
    {
        public IncidentDef incident;
        public List<FactionDef> playerFactions;
        public FactionDef factionRequirementInWorlds;
        
        public int minColonyWealth = -1;

        public float refireIntervalDays = -1f;

        public StorytellerCompProperties_DreamumVictory()
        {
            compClass = typeof(StorytellerCompDreamumVictory);
        }
    }

    public class StorytellerCompDreamumVictory : StorytellerComp
    {
        public StorytellerCompProperties_DreamumVictory Props => (StorytellerCompProperties_DreamumVictory)props;

        protected int IntervalsPassed => Find.TickManager.TicksGame / 1000;

        public override string ToString()
        {
            return base.ToString() + " " + Props.incident;
        }

        public override IEnumerable<FiringIncident> MakeIntervalIncidents(IIncidentTarget target)
        {
            if (!Props.incident.TargetAllowed(target))
            {
                yield break;
            }

            if (!Props.playerFactions.Contains(Find.FactionManager.OfPlayer.def))
            {
                yield break;
            }

            if (!Find.Storyteller.difficulty.allowViolentQuests)
            {
                yield break;
            }

            var npcFaction = Find.FactionManager.AllFactionsListForReading.FirstOrDefault(v => v.def == Props.factionRequirementInWorlds);
            if (npcFaction == null || npcFaction.PlayerRelationKind == FactionRelationKind.Hostile)
            {
                yield break;
            }

            if (Props.minColonyWealth > 0 && WealthUtility.PlayerWealth < Props.minColonyWealth)
            {
                yield break;
            }

            foreach (var endedQuest in Find.QuestManager.QuestsListForReading.Where(
                q =>
                q.root == Props.incident.questScriptDef &&
                q.State != QuestState.NotYetAccepted &&
                q.State != QuestState.EndedSuccess &&
                q.State != QuestState.Ongoing &&
                q.cleanupTick >= 0))
            {
                if (GenTicks.TicksGame < endedQuest.cleanupTick + Props.refireIntervalDays * 60000)
                {
                    yield break;
                }
            }

            var parms = GenerateParms(Props.incident.category, target);
            if (Props.incident.Worker.CanFireNow(parms))
            {
                yield return new FiringIncident(Props.incident, this, parms);
            }
        }
    }
}
