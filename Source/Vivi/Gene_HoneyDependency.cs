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

            if (pawn.kindDef != null)
            {
                var hediff = (Hediff_HoneyDependency)HediffMaker.MakeHediff(VVHediffDefOf.VV_HoneyNeed, pawn);
                pawn.health.AddHediff(hediff);
            }

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
            if (IsFoodContainingHoney(thing))
            {
                Reset();
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

        public static bool IsFoodContainingHoney(Thing thing)
        {
            var compIngredients = thing.TryGetComp<CompIngredients>();
            if (compIngredients != null)
            {
                for (int i = 0; i < compIngredients.ingredients.Count; ++i)
                {
                    var ingredientDef = compIngredients.ingredients[i];
                    if (IsFoodContainingHoney(ingredientDef))
                    {
                        return true;
                    }
                }
            }

            if (IsFoodContainingHoney(thing.def))
            {
                return true;
            }

            return false;
        }

        public static bool IsFoodContainingHoney(ThingDef thingDef)
        {
            if (thingDef.GetCompProperties<CompProperties_Honey>() != null)
            {
                return true;
            }

            var drugCompProps = thingDef.GetCompProperties<CompProperties_Drug>();
            if (drugCompProps != null && drugCompProps.chemical == VVChemicalDefOf.Ambrosia)
            {
                return true;
            }

            return false;
        }
    }
}
