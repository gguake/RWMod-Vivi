using UnityEngine;
using Verse;

namespace VVRace
{
    [StaticConstructorOnStartup]
    public class ArcanePlant_Everflower : ArcanePlant, IGatherableTarget
    {
        public bool CanGatherByPawn(Pawn pawn, RecipeDef_Gathering recipe)
        {
            if (!pawn.IsVivi() || pawn.ageTracker.AgeBiologicalYears < 13) { return false; }

            if (pawn.health.hediffSet.HasHediff(VVHediffDefOf.VV_EverflowerImpact))
            {
                return false;
            }

            return true;
        }

        public void Notify_Gathered(Pawn pawn, RecipeDef_Gathering recipe)
        {
            if (pawn.IsVivi())
            {
                var ticks = Mathf.Clamp(Rand.Range(60000 * 3, 60000 * 10), 0, (int)(pawn.ageTracker.AgeBiologicalTicks - 13 * 60000 * 60)) - 1;
                if (ticks > 0)
                {
                    pawn.ageTracker.AgeBiologicalTicks -= ticks;
                    ManaFluxNode.mana = Mathf.Clamp(ManaFluxNode.mana + ticks / 60000f * 25, 0, ManaExtension.manaCapacity);

                    pawn.health.AddHediff(VVHediffDefOf.VV_EverflowerImpact);
                }
            }
        }
    }
}
