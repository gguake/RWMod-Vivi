using Verse;

namespace VVRace
{
    public class PawnRenderNode_SwaddleVivi : PawnRenderNode_Swaddle
    {
        public PawnRenderNode_SwaddleVivi(Pawn pawn, PawnRenderNodeProperties props, PawnRenderTree tree) : base(pawn, props, tree)
        {
        }

        protected override string TexPathFor(Pawn pawn)
        {
            if (pawn.IsVivi())
            {
                return "Things/Pawn/Vivi/Swaddle/Swaddle";
            }

            return base.TexPathFor(pawn);
        }
    }
}
