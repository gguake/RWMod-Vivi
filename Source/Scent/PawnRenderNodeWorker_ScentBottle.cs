using RPEF;
using UnityEngine;
using Verse;

namespace VVRace
{
    // 착용 아트가 Thin 기준이므로 다른 체형은 RPEF의 체형별 offset으로 보정한다.
    public class PawnRenderNodeWorker_ScentBottle : PawnRenderNodeWorker_Apparel_Body
    {
        public override Vector3 OffsetFor(PawnRenderNode node, PawnDrawParms parms, out Vector3 pivot)
        {
            var offset = base.OffsetFor(node, parms, out pivot);

            if (node.Props is PawnRenderNodeProperties_BodyTypeDrawData props && props.bodyTypeDrawData != null)
            {
                var bodyType = parms.pawn.story?.bodyType;
                if (bodyType != null)
                {
                    for (int i = 0; i < props.bodyTypeDrawData.Count; ++i)
                    {
                        var entry = props.bodyTypeDrawData[i];
                        if (entry.bodyType == bodyType && entry.drawData != null)
                        {
                            offset += entry.drawData.OffsetForRot(parms.facing);
                            break;
                        }
                    }
                }
            }

            return offset;
        }
    }
}
