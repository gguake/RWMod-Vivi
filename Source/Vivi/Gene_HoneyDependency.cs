using RimWorld;
using Verse;

namespace VVRace
{
    public class Gene_HoneyDependency : Gene
    {
        public int lastIngestedTick;

        [Unsaved(false)]
        private Hediff_HoneyDependency _cachedLinkedHediff;
        public Hediff_HoneyDependency LinkedHediff
        {
            get
            {
                if (_cachedLinkedHediff == null && pawn.health?.hediffSet?.hediffs != null)
                {
                    var hediffs = pawn.health.hediffSet.hediffs;
                    for (int i = 0; i < hediffs.Count; i++)
                    {
                        if (hediffs[i] is Hediff_HoneyDependency honeyHediff)
                        {
                            _cachedLinkedHediff = honeyHediff;
                            break;
                        }
                    }
                }

                return _cachedLinkedHediff;
            }
        }

        public override void PostAdd()
        {
            base.PostAdd();

            var hediff = (Hediff_HoneyDependency)HediffMaker.MakeHediff(VVHediffDefOf.VV_HoneyNeed, pawn);
            pawn.health.AddHediff(hediff);

            lastIngestedTick = Find.TickManager.TicksGame;
        }

        public override void PostRemove()
        {
            var linkedHediff = LinkedHediff;
            if (linkedHediff != null)
            {
                pawn.health.RemoveHediff(linkedHediff);
            }

            base.PostRemove();
        }

        public override void Notify_IngestedThing(Thing thing, int numTaken)
        {
            var compIngredients = thing.TryGetComp<CompIngredients>();
            if (compIngredients != null)
            {
                for (int i = 0; i < compIngredients.ingredients.Count; ++i)
                {
                    if (compIngredients.ingredients[i].GetCompProperties<CompProperties_Honey>() != null)
                    {
                        Reset();
                        return;
                    }
                }
            }
            else
            {
                if (thing.TryGetComp<CompHoney>() != null)
                {
                    Reset();
                }
            }
        }

        public override void Reset()
        {
            var linkedHediff = LinkedHediff;
            if (linkedHediff != null)
            {
                linkedHediff.Severity = linkedHediff.def.initialSeverity;
            }

            lastIngestedTick = Find.TickManager.TicksGame;
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref lastIngestedTick, "lastIngestedTick", 0);
        }
    }
}
