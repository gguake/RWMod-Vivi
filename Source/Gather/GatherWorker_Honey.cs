using RimWorld;
using Verse;

namespace VVRace
{
    public class GatherWorker_Honey : GatherWorker_Plant
    {
        public override void Notify_Gathered(Pawn pawn, Thing billGiver, Thing target)
        {
            base.Notify_Gathered(pawn, billGiver, target);

            if (pawn.filth != null && Rand.Bool)
            {
                pawn.filth.GainFilth(VVThingDefOf.VV_FilthPollen, Gen.YieldSingle(target.def.defName));
            }
            else if (Rand.Bool)
            {
                FilthMaker.TryMakeFilth(pawn.Position, pawn.Map, VVThingDefOf.VV_FilthPollen, target.def.defName, 1);
            }
        }
    }
}
