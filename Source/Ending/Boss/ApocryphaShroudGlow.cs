using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace VVRace
{
    [StaticConstructorOnStartup]
    internal static class ApocryphaShroudGlow
    {
        private const string ShaderPath = "Map/VV_BodyGlow";

        private static readonly int CoreId = Shader.PropertyToID("_Core");
        private static readonly int BloomId = Shader.PropertyToID("_Bloom");
        private static readonly int SpreadId = Shader.PropertyToID("_Spread");
        private static readonly int ExpandId = Shader.PropertyToID("_Expand");

        private static readonly Dictionary<Texture, Material> GlowMaterials = new Dictionary<Texture, Material>();
        private static readonly MaterialPropertyBlock PropertyBlock = new MaterialPropertyBlock();
        private static readonly Color GlowColor = new Color(1f, 0.88f, 0.62f);

        private const float CoreDay = 0.16f;
        private const float CoreNight = 0.6f;
        private const float BloomDay = 0.5f;
        private const float BloomNight = 0.85f;
        private const float PulseAmount = 0.12f;
        private const float PulseSpeed = 1.3f;
        private const float Spread = 0.05f;
        private const float Expand = 1.12f;

        public static void Draw(Pawn pawn)
        {
            var graphic = pawn.ageTracker.CurKindLifeStage?.bodyGraphicData?.Graphic;
            if (graphic == null) { return; }

            var rot = pawn.Rotation;
            var texture = graphic.MatAt(rot)?.mainTexture;
            if (texture == null) { return; }

            if (!GlowMaterials.TryGetValue(texture, out var material))
            {
                material = MaterialPool.MatFrom(new MaterialRequest(texture, ShaderDatabase.LoadShader(ShaderPath), Color.white));
                GlowMaterials[texture] = material;
            }

            var dark = 1f - Mathf.Clamp01(pawn.Map.glowGrid.GroundGlowAt(pawn.Position));
            var pulse = 1f + PulseAmount * Mathf.Sin(Find.TickManager.TicksGame / 60f * PulseSpeed);

            PropertyBlock.Clear();
            PropertyBlock.SetColor(ShaderPropertyIDs.Color, GlowColor);
            PropertyBlock.SetFloat(CoreId, Mathf.Lerp(CoreDay, CoreNight, dark) * pulse);
            PropertyBlock.SetFloat(BloomId, Mathf.Lerp(BloomDay, BloomNight, dark) * pulse);
            PropertyBlock.SetFloat(SpreadId, Spread);
            PropertyBlock.SetFloat(ExpandId, Expand);

            var position = pawn.DrawPos;
            position.y += 0.02f;
            var matrix = Matrix4x4.TRS(position, Quaternion.identity, new Vector3(Expand, 1f, Expand));
            Graphics.DrawMesh(graphic.MeshAt(rot), matrix, material, 0, null, 0, PropertyBlock);
        }
    }
}
