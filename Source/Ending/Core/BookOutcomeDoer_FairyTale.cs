using RimWorld;
using System;
using Verse;

namespace VVRace
{
    public class BookOutcomeProperties_FairyTale : BookOutcomeProperties
    {
        public override Type DoerClass => typeof(BookOutcomeDoer_FairyTale);
    }

    public class BookOutcomeDoer_FairyTale : BookOutcomeDoer
    {
        public override bool DoesProvidesOutcome(Pawn reader)
        {
            var ending = ViviEndingQuest.Current;
            return ending != null && !ending.Ruins.HasCoordinates && ending.Tales.ReadingProgress < 1f;
        }

        public override void OnReadingTick(Pawn reader, float factor)
        {
            if (factor <= 0f) { return; }

            ViviEndingQuest.Current?.Tales.AddReadingProgress(factor);
        }
    }
}
