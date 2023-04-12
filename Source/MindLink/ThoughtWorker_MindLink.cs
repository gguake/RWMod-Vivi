using RimWorld;
using RimWorld.Planet;
using Verse;

namespace VVRace
{
    public class ThoughtWorker_MindLink : ThoughtWorker
    {
        protected override ThoughtState CurrentStateInternal(Pawn pawn)
        {
            if (pawn.TryGetMindLinkMaster(out var master) && master.needs?.mood?.thoughts != null)
            {
                var shouldActiveLink = pawn.IsCaravanMember() ? pawn.GetCaravan().pawns.Contains(master) : pawn.MapHeld == master.MapHeld;
                var curInstantLevelPercentage = master.needs.mood.CurInstantLevelPercentage;
                if (shouldActiveLink && curInstantLevelPercentage > master.needs.mood.CurLevelPercentage + 0.05f)
                {
                    return ThoughtState.ActiveAtStage(0);
                }
                else if (shouldActiveLink && curInstantLevelPercentage < master.needs.mood.CurLevelPercentage - 0.05f)
                {
                    return ThoughtState.ActiveAtStage(1);
                }
            }

            return false;
        }

        public override float MoodMultiplier(Pawn pawn)
        {
            var def = (ThoughtDef_MindLink)this.def;
            if (pawn.TryGetViviGene(out var vivi) && pawn.TryGetMindLinkMaster(out var master) && !master.Dead && master.needs?.mood?.thoughts != null)
            {
                var mindLinkConnectedTicks = vivi.ViviMindLinkSettings?.HediffMindLink?.ConnectedTicks ?? 0;
                foreach (var stage in def.mindLinkStages)
                {
                    if (mindLinkConnectedTicks >= stage.mindLinkElapsedTicks)
                    {
                        return stage.moodMultiplier;
                    }
                }
            }

            return 0f;
        }
    }
}
