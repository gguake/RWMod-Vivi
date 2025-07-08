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
                var ticks = Mathf.Clamp(
                    Mathf.CeilToInt(Rand.Range(60000 * 5, 60000 * 10) * pawn.ageTracker.BiologicalTicksPerTick), 
                    0, 
                    (int)(pawn.ageTracker.AgeBiologicalTicks - 13 * 60000 * 60)) - 1;

                if (ticks > 0)
                {
                    pawn.ageTracker.AgeBiologicalTicks -= ticks;

                    Map.GetManaComponent().ChangeEnvironmentMana(Position, ticks / 60000f * 50);
                    pawn.health.AddHediff(VVHediffDefOf.VV_EverflowerImpact);
                }
            }
        }
    }
}
