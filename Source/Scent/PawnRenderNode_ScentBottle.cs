using System.Collections.Generic;
using RimWorld;
using RPEF;
using UnityEngine;
using Verse;

namespace VVRace
{
    // 향수병 착용 렌더 노드. 향 상태에 따라 빈 병/채워진 병 텍스처를 전환하고,
    // 채워진 병에는 ScentColor 틴트를 적용한다.
    // 부모 노드 연결과 레이어 배치는 RPEF의 ProcessApparel postfix
    // (PawnRenderNode_ApparelBase 감지)가 처리한다.
    public class PawnRenderNode_ScentBottle : PawnRenderNode_ApparelBase
    {
        public PawnRenderNode_ScentBottle(Pawn pawn, PawnRenderNodeProperties props, PawnRenderTree tree)
            : base(pawn, props, tree)
        {
        }

        protected override IEnumerable<Graphic> GraphicsFor(Pawn pawn)
        {
            if (apparel == null) { yield break; }

            var comp = apparel.TryGetComp<CompScentBottle>();
            if (comp == null) { yield break; }

            var filled = comp.HasScent;
            var root = filled ? comp.Props.filledWornGraphicPath : comp.Props.emptyWornGraphicPath;

            yield return GraphicDatabase.Get<Graphic_Multi>(
                ResolveWornPath(root, ResolveBodyType(pawn)),
                filled ? ShaderDatabase.CutoutComplex : ShaderDatabase.Cutout,
                apparel.def.graphicData.drawSize,
                filled ? apparel.DrawColor : Color.white);
        }

        // 바닐라 착용 그래픽 경로(TryGetGraphicApparel)에 적용되는 RPEF.ApparelGraphicHook의
        // 체형 오버라이드를 동일하게 재현한다. 커스텀 노드는 그 경로를 우회하므로
        // 여기서 직접 적용하지 않으면 hook이 무시된다.
        private BodyTypeDef ResolveBodyType(Pawn pawn)
        {
            var bodyType = pawn.story?.bodyType;

            var hook = apparel.def.GetModExtension<ApparelGraphicHook>();
            if (hook == null) { return bodyType; }

            if (hook.bodyTypeGraphicOverride != null)
            {
                foreach (var overrideEntry in hook.bodyTypeGraphicOverride)
                {
                    if (overrideEntry.from == bodyType)
                    {
                        return overrideEntry.to;
                    }
                }
            }

            return hook.defaultBodyTypeGraphicOverride ?? bodyType;
        }

        // "{경로}_{체형}" 접미사 규칙을 재현하되, 해당 체형 텍스처가 없으면
        // 루트 단일 텍스처로 폴백한다.
        private static string ResolveWornPath(string root, BodyTypeDef bodyType)
        {
            if (bodyType != null && HasWornTexture(root + "_" + bodyType.defName))
            {
                return root + "_" + bodyType.defName;
            }

            return root;
        }

        private static bool HasWornTexture(string path)
        {
            return ContentFinder<Texture2D>.Get(path + "_north", false) != null ||
                ContentFinder<Texture2D>.Get(path, false) != null;
        }
    }
}
