using RimWorld;
using System.Text;
using Verse;

namespace VVRace
{
    public class Hediff_HoneyDependency : HediffWithComps
    {
        public override bool ShouldRemove => false;

        [Unsaved(false)]
        private Gene_HoneyDependency _cachedLinkedGene;
        public Gene_HoneyDependency LinkedGene
        {
            get
            {
                if (_cachedLinkedGene == null && pawn.genes != null)
                {
                    var genes = pawn.genes.GenesListForReading;
                    for (int i = 0; i < genes.Count; i++)
                    {
                        if (genes[i] is Gene_HoneyDependency gene)
                        {
                            _cachedLinkedGene = gene;
                            break;
                        }
                    }
                }
                return _cachedLinkedGene;
            }
        }

        public override string TipStringExtra
        {
            get
            {
                var sb = new StringBuilder(base.TipStringExtra);
                var linkedGene = LinkedGene;
                if (linkedGene != null)
                {
                    if (sb.Length > 0)
                    {
                        sb.AppendLine();
                    }

                    sb.AppendInNewLine(LocalizeTexts.GeneDefHoneyNeedDurationDesc.Translate(pawn.Named("PAWN"), "PeriodDays".Translate(5f).Named("DEFICIENCYDURATION")).Resolve());
                    sb.AppendInNewLine(LocalizeTexts.LastHoneyIngestedDurationAgo.Translate((Find.TickManager.TicksGame - linkedGene.lastIngestedTick).ToStringTicksToPeriod().Named("DURATION")).Resolve());
                }

                return sb.ToString();
            }
        }

        public override bool TryMergeWith(Hediff other)
        {
            return other is Hediff_HoneyDependency honeyDependencyHediff;
        }

    }
}
