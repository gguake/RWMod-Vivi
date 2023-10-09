using RimWorld;
using System.Collections.Generic;
using System.Linq;
using Verse;

namespace VVRace
{
    public class StorytellerCompProperties_WorldtreeQuest : StorytellerCompProperties
    {
        public IncidentDef incident;
        public float refireEveryDays = -1f;
        public int minColonyWealth = -1;
        public ResearchProjectDef targetResearchProjDef;
        public FactionDef playerFactionDef;

        public StorytellerCompProperties_WorldtreeQuest()
        {
            compClass = typeof(StorytellerComp_WorldtreeQuest);
        }
    }

    public class StorytellerComp_WorldtreeQuest : StorytellerComp
    {
        private StorytellerCompProperties_WorldtreeQuest Props => (StorytellerCompProperties_WorldtreeQuest)props;

        private int IntervalsPassed => Find.TickManager.TicksGame / 1000;
        private bool generateSkipped;

        public override IEnumerable<FiringIncident> MakeIntervalIncidents(IIncidentTarget target)
        {
            if (!Find.Storyteller.difficulty.allowViolentQuests)
            {
                yield break;
            }

            if (!Props.incident.TargetAllowed(target))
            {
                yield break;
            }

            if (Props.targetResearchProjDef != null && !Props.targetResearchProjDef.IsFinished)
            {
                yield break;
            }

            if (Props.playerFactionDef != null && Find.FactionManager.OfPlayer.def != Props.playerFactionDef)
            {
                yield break;
            }

            if (!Current.Game.AnyPlayerHomeMap.PlayerPawnsForStoryteller.Any(pawn => pawn.IsColonist && pawn.IsRoyalVivi()))
            {
                yield break;
            }

            var quest = Find.QuestManager.QuestsListForReading.FirstOrDefault(v => v.root == Props.incident.questScriptDef);
            if (quest == null)
            {
                var doGenerateQuest = false;
                if ((Props.minColonyWealth <= 0 || WealthUtility.PlayerWealth >= Props.minColonyWealth) && (!generateSkipped ? (IntervalsPassed == (int)(Props.minDaysPassed * 60f) + 1) : (GenTicks.TicksGame >= Props.minDaysPassed * 60000f)))
                {
                    doGenerateQuest = true;
                }
                else if (Props.refireEveryDays >= 0f && (quest.State != QuestState.EndedSuccess && quest.State != QuestState.Ongoing && quest.State != 0 && quest.cleanupTick >= 0 && IntervalsPassed == (int)(quest.cleanupTick + Props.refireEveryDays * 60000f) / 1000))
                {
                    doGenerateQuest = true;
                }

                if (doGenerateQuest)
                {
                    var parms = GenerateParms(Props.incident.category, target);
                    if (Props.incident.Worker.CanFireNow(parms))
                    {
                        yield return new FiringIncident(Props.incident, this, parms);
                    }
                }
            }
        }

        public override void Initialize()
        {
            base.Initialize();
            if ((float)GenTicks.TicksGame >= Props.minDaysPassed * 60000f)
            {
                generateSkipped = true;
            }
        }

        public override string ToString()
        {
            return base.ToString() + " " + Props.incident;
        }
    }
}
