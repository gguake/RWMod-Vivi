using UnityEngine;
using Verse;

namespace VVRace
{
    public class PawnRenderNodeProperties_ViviCustomDrawSize : PawnRenderNodeProperties_Parent
    {
        public float normalViviDrawSize;
        public float royalViviDrawSize;
    }

    public class PawnRenderNodeWorker_ViviCustomDrawSize : PawnRenderNodeWorker
    {
        public override Vector3 ScaleFor(PawnRenderNode node, PawnDrawParms parms)
        {
            var scale = base.ScaleFor(node, parms);

            var props = node.Props as PawnRenderNodeProperties_ViviCustomDrawSize;
            if (props == null) { return scale; }

            if (parms.pawn.IsVivi())
            {
                if (parms.pawn.IsRoyalVivi())
                {
                    scale *= props.royalViviDrawSize;
                }
                else
                {
                    scale *= props.normalViviDrawSize;
                }
            }

            return scale;
        }
    }
}
