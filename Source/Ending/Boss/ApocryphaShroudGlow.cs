using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace VVRace
{
    [StaticConstructorOnStartup]
    internal static class ApocryphaShroudGlow
    {
        private static readonly Dictionary<Texture, Material> GlowMaterials = new Dictionary<Texture, Material>();
        private static readonly MaterialPropertyBlock PropertyBlock = new MaterialPropertyBlock();
        private static readonly Color GlowColor = new Color(1f, 0.86f, 0.55f);

        private const float AlphaBase = 0.3f;
        private const float AlphaPulse = 0.12f;
        private const float PulseSpeed = 1.6f;
        private const float DaylightAlphaFactor = 0.15f;

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
