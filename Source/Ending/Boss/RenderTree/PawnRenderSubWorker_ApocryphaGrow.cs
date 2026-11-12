using RimWorld;
using UnityEngine;
using Verse;

namespace VVRace
{
    // 싱글턴 서브워커의 TransformScale·EditMaterial은 병렬 실행되므로 인스턴스 필드·Rand·Find를 변경하지 않는다.
    // 컴프의 저장 필드와 미리 준비된 재질만 읽는다. EditMaterialPropertyBlock은 메인 스레드에서 그리기 직전에 호출된다.
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
