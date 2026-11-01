using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace VVRace
{
    // 밤에도 몸통이 빛나도록 같은 텍스처를 MoteGlow 가산 혼합으로 한 번 더 그린다.
    // 낮에는 흰 모피와 피부가 금빛으로 바래지 않도록 바닥 밝기에 따라 발광을 줄인다.
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

            var position = pawn.DrawPos;
            position.y += 0.02f;
            Graphics.DrawMesh(graphic.MeshAt(rot), position, Quaternion.identity, material, 0, null, 0, PropertyBlock);
        }
    }
}
