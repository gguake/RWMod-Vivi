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

            var compVivi = parms.pawn.GetCompVivi();
            if (compVivi != null)
            {
                scale *= compVivi.isRoyal ? props.royalViviDrawSize : props.normalViviDrawSize;
            }

            return scale;
        }
    }
}
