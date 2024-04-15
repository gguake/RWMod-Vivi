using Verse;

namespace VVRace
{
    public class HediffComp_ImmunizableDreamum : HediffComp_Immunizable
    {
        public override float SeverityChangePerDay()
        {
            var change = base.SeverityChangePerDay();
            if (change < 0f && Pawn.Spawned)
            {
                if (Pawn.Map.gameConditionManager.GetActiveCondition<GameCondition_DreamumHaze>() != null)
                {
                    change = 0f;
                }
            }

            return change;
        }
    }
}
