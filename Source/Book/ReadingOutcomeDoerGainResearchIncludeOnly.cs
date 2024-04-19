using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using Verse;

namespace VVRace
{
    public class BookOutcomeProperties_GainResearchIncludeOnly : BookOutcomeProperties_GainResearch
    {
        public override Type DoerClass => typeof(ReadingOutcomeDoerGainResearchIncludeOnly);
    }

    public class ReadingOutcomeDoerGainResearchIncludeOnly : ReadingOutcomeDoerGainResearch
    {
        public override void OnBookGenerated(Pawn author = null)
        {
            var unifinishedProjects = new List<ResearchProjectDef>();
            var allProjects = new List<ResearchProjectDef>();
            if (Props.include.Count > 0)
            {
                for (int j = 0; j < Props.include.Count; j++)
                {
                    var project = Props.include[j].project;
                    if (!project.IsFinished)
                    {
                        unifinishedProjects.Add(project);
                    }

                    allProjects.Add(project);
                }
            }

            if (allProjects.Empty())
            {
                Log.ErrorOnce($"Research book had no valid projects and failed to find any backup projects (tabs: {Props.tabs.Select((BookOutcomeProperties_GainResearch.BookTabItem x) => x.tab.generalTitle).ToCommaList()}. ignoring zero base cost: {Props.ignoreZeroBaseCost})", 15242436);
                return;
            }

            var selected = unifinishedProjects.Count > 0 ? unifinishedProjects.RandomElement() : allProjects.RandomElement();
            var value = GetBaseValue();

            values[selected] = value;
        }
    }
}
