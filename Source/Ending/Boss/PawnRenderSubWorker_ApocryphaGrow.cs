using RimWorld;
using UnityEngine;
using Verse;

namespace VVRace
{
    // 2페이즈 전환 동안의 몸통 연출.
    // - 크기: 6.5 → 8.5(1.0 → 1.3배)로 서서히 키우고, 심장 박동마다 옆으로 부풀었다 꺼지게 한다.
    //   실제 메시는 생애 단계 그래픽(drawSize)을 따르므로, 지금 메시 크기 대비 목표 크기의 비율만 곱해 중간에 그래픽이 바뀌어도 크기가 이어지게 한다.
    // - 재질: 몸통을 VV_Morph 모핑 재질로 바꿔 1페이즈 몸이 꿈틀거리며 2페이즈 몸으로 뒤바뀌게 하고, 매 프레임 경과 시간을 넣는다.
    // 서브워커는 싱글턴이며 TransformScale·EditMaterial은 병렬(IJobParallelFor)로 실행되므로 인스턴스 필드·Rand·Find를 변경하지 않고
    // 컴프의 저장 필드와 메인 스레드에서 미리 만든 재질만 읽는다. EditMaterialPropertyBlock은 메인 스레드에서 그리기 직전에 호출된다.
    public class PawnRenderSubWorker_ApocryphaGrow : PawnRenderSubWorker
    {
        private const float BeatWidthSwell = 0.045f;
        private const float BeatHeightSquash = 0.03f;

        public override void TransformScale(PawnRenderNode node, PawnDrawParms parms, ref Vector3 scale)
        {
            var comp = parms.pawn?.TryGetComp<CompApocrypha>();
            if (comp == null) { return; }

            var meshSize = node.PrimaryGraphic?.drawSize.x ?? 0f;
            if (meshSize <= 0f) { return; }

            var targetSize = comp.CurrentVisualDrawSize;
            if (targetSize <= 0f) { return; }

            var factor = targetSize / meshSize;
            scale.x *= factor;
            scale.z *= factor;

            if (comp.Phase == ApocryphaPhase.Transforming)
            {
                var swell = ApocryphaMorphMaterials.Beat(comp.TransformAgeSecs) * Mathf.Sin(Mathf.PI * comp.TransformProgress);
                scale.x *= 1f + BeatWidthSwell * swell;
                scale.z *= 1f - BeatHeightSquash * swell;
            }
        }

        public override void EditMaterial(PawnRenderNode node, PawnDrawParms parms, ref Material material)
        {
            if (material == null || parms.Portrait) { return; }

            var comp = parms.pawn?.TryGetComp<CompApocrypha>();
            if (comp == null || comp.Phase != ApocryphaPhase.Transforming) { return; }

            var morph = ApocryphaMorphMaterials.Get(parms.facing);
            if (morph != null)
            {
                material = morph;
            }
        }

        public override void EditMaterialPropertyBlock(PawnRenderNode node, Material material, PawnDrawParms parms, ref MaterialPropertyBlock block)
        {
            if (block == null || material == null || material != ApocryphaMorphMaterials.Get(parms.facing)) { return; }

            var comp = parms.pawn?.TryGetComp<CompApocrypha>();
            if (comp == null) { return; }

            block.SetFloat(ShaderPropertyIDs.AgeSecs, comp.TransformAgeSecs);
        }
    }
}
