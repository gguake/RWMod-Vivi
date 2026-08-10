using RPEF;
using UnityEngine;
using Verse;

namespace VVRace
{
    // Apparel_Body 워커의 의복 동작(의복 숨김 조건, 눕기/기어가기 처리 등)을 유지하면서
    // RPEF PawnRenderNodeProperties_BodyTypeDrawData의 체형별 offset 보정만 더한다.
    // Thin 기준으로 그려진 착용 아트를 다른 체형에서 위치 보정하는 용도이며,
    // 엔트리가 없는 체형(Thin 등)은 보정 없이 기본 동작을 따른다.
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
