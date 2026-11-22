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
        private static readonly Color HaloColor = new Color(1f, 0.87f, 0.58f);
        private static readonly Material HaloMat = new Material(MaterialPool.MatFrom("VFX/VV_ScentAura", ShaderDatabase.MoteGlow, Color.white));

        private const float AlphaBase = 0.3f;
        private const float AlphaPulse = 0.12f;
        private const float PulseSpeed = 1.6f;
        private const float DaylightAlphaFactor = 0.15f;

        private const float HaloSize = 6f;
        private const float HaloSizePulse = 0.15f;
        private const float HaloAlpha = 0.65f;
        private const float HaloAlphaPulse = 0.06f;
        private const float HaloDaylightFactor = 1.5f;
        private const float HaloBehindAltitude = 0.035f;

        public static void Draw(Pawn pawn)
        {
            var graphic = pawn.ageTracker.CurKindLifeStage?.bodyGraphicData?.Graphic;
            if (graphic == null) { return; }

            var rot = pawn.Rotation;
            var bodyMat = graphic.MatAt(rot);
            DrawHalo(pawn, bodyMat);

            var texture = bodyMat?.mainTexture;
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

        private static void DrawHalo(Pawn pawn, Material bodyMat)
        {
            if (bodyMat != null && HaloMat.renderQueue != bodyMat.renderQueue - 1)
            {
                HaloMat.renderQueue = bodyMat.renderQueue - 1;
            }

            var wave = Mathf.Sin(Find.TickManager.TicksGame / 60f * PulseSpeed * 0.5f);
            var light = pawn.Map.glowGrid.GroundGlowAt(pawn.Position);
            var color = HaloColor;
            color.a = (HaloAlpha + HaloAlphaPulse * wave) * Mathf.Lerp(1f, HaloDaylightFactor, light);

            var size = HaloSize * (1f + HaloSizePulse * 0.5f * wave);
            var position = ApocryphaGimmicks.GetBossBodyPos(pawn);
            position.y = pawn.DrawPos.y - HaloBehindAltitude;

            PropertyBlock.Clear();
            PropertyBlock.SetColor(ShaderPropertyIDs.Color, color);
            Graphics.DrawMesh(MeshPool.plane10, Matrix4x4.TRS(position, Quaternion.identity, new Vector3(size, 1f, size)), HaloMat, 0, null, 0, PropertyBlock);
        }
    }
}
