using System.Collections.Generic;
using RimWorld;
using RPEF;
using UnityEngine;
using Verse;

namespace VVRace
{
    // 부모 연결과 레이어 배치는 PawnRenderNode_ApparelBase를 감지하는 RPEF의 ProcessApparel 후처리가 맡는다.
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

        // 이 노드는 TryGetGraphicApparel 경로를 우회하므로 RPEF.ApparelGraphicHook의 체형 보정을 직접 적용한다.
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
