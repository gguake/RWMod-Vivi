using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace VVRace
{
    // 1페이즈 아포크리파(여왕 모습)의 은은한 발광.
    // 몸통이 맵 조명을 받아 밤에는 어둡게 바래 보이므로, 같은 몸통 텍스처를 가산 혼합(MoteGlow)으로
    // 은은하게 맥동시키며 한 번 더 그린다. 낮에는 흰 모피·피부까지 금빛으로 바래므로 바닥 밝기만큼 줄인다.
    [StaticConstructorOnStartup]
    internal static class ApocryphaShroudGlow
    {
        private static readonly Dictionary<Texture, Material> GlowMaterials = new Dictionary<Texture, Material>();
        private static readonly MaterialPropertyBlock PropertyBlock = new MaterialPropertyBlock();
        private static readonly Color GlowColor = new Color(1f, 0.86f, 0.55f);

        private const float AlphaBase = 0.3f;
        private const float AlphaPulse = 0.12f;
        private const float PulseSpeed = 1.6f;
        private const float DaylightAlphaFactor = 0.15f;   // 바닥 밝기 1(한낮)일 때 남기는 비율

        public static void Draw(Pawn pawn)
        {
            var graphic = pawn.ageTracker.CurKindLifeStage?.bodyGraphicData?.Graphic;
            if (graphic == null) { return; }

            var rot = pawn.Rotation;
            var texture = graphic.MatAt(rot)?.mainTexture;
            if (texture == null) { return; }

            if (!GlowMaterials.TryGetValue(texture, out var material))
            {
                material = MaterialPool.MatFrom(new MaterialRequest(texture, ShaderDatabase.MoteGlow, Color.white));
                GlowMaterials[texture] = material;
            }

            var color = GlowColor;
            var light = pawn.Map.glowGrid.GroundGlowAt(pawn.Position);
            color.a = (AlphaBase + AlphaPulse * Mathf.Sin(Find.TickManager.TicksGame / 60f * PulseSpeed)) * Mathf.Lerp(1f, DaylightAlphaFactor, light);

            PropertyBlock.Clear();
            PropertyBlock.SetColor(ShaderPropertyIDs.Color, color);

            // 몸통과 같은 메시(서쪽은 좌우 반전 메시)로, 몸통 바로 위 높이에 그린다.
            var position = pawn.DrawPos;
            position.y += 0.02f;
            Graphics.DrawMesh(graphic.MeshAt(rot), position, Quaternion.identity, material, 0, null, 0, PropertyBlock);
        }
    }
}
