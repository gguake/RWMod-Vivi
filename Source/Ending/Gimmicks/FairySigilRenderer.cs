using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace VVRace
{
    [StaticConstructorOnStartup]
    public static class FairySigilRenderer
    {
        public const float LineHalfWidth = 0.3f;
        public const float DefaultFaintAlpha = 0.12f;

        public static readonly Color DefaultColor = new Color(1f, 0.87f, 0.6f, 1f);

        private static readonly Material SigilMat = MaterialPool.MatFrom(BaseContent.WhiteTex, ShaderDatabase.LoadShader("Map/VV_FairySigil"), Color.white);
        private static readonly Material GlowMat = MaterialPool.MatFrom("VFX/VV_ScentAura", ShaderDatabase.MoteGlow, Color.white);
        private static readonly Material TwinkleMat = MaterialPool.MatFrom("VFX/VV_ScentTwinkle", ShaderDatabase.MoteGlow, Color.white);
        private static readonly MaterialPropertyBlock PropertyBlock = new MaterialPropertyBlock();
        private static readonly Dictionary<(FairySigilLanes, int), Mesh> MeshCache = new Dictionary<(FairySigilLanes, int), Mesh>();

        private static readonly int ProgressId = Shader.PropertyToID("_Progress");
        private static readonly int IntensityId = Shader.PropertyToID("_Intensity");
        private static readonly int FlashId = Shader.PropertyToID("_Flash");
        private static readonly int LaneLengthId = Shader.PropertyToID("_LaneLength");
        private static readonly int HalfWidthId = Shader.PropertyToID("_HalfWidth");
        private static readonly int FaintAlphaId = Shader.PropertyToID("_FaintAlpha");

        private static readonly List<Vector3> Vertices = new List<Vector3>();
        private static readonly List<Vector2> Uvs = new List<Vector2>();
        private static readonly List<Color32> Colors = new List<Color32>();
        private static readonly List<int> Triangles = new List<int>();
        private static readonly List<SigilSpanPoint> Span = new List<SigilSpanPoint>();

        public static void Draw(Vector3 center, FairySigil sigil, int fairyCount, float progress, float intensity = 1f, float flash = 0f, Color? color = null, float radius = FairySigil.WorldRadius, float faintAlpha = DefaultFaintAlpha)
        {
            if (sigil == null || intensity <= 0f) { return; }

            var lanes = sigil.GetLanes(fairyCount);
            var mesh = GetMesh(lanes, radius);
            var tint = color ?? DefaultColor;

            PropertyBlock.Clear();
            PropertyBlock.SetColor(ShaderPropertyIDs.Color, tint);
            PropertyBlock.SetFloat(ShaderPropertyIDs.AgeSecs, LinkRenderUtility.WrappedAgeSecs);
            PropertyBlock.SetFloat(ProgressId, progress >= 1f ? 1.05f : progress);
            PropertyBlock.SetFloat(IntensityId, intensity);
            PropertyBlock.SetFloat(FlashId, Mathf.Clamp01(flash));
            PropertyBlock.SetFloat(LaneLengthId, lanes.AverageLength * radius);
            PropertyBlock.SetFloat(HalfWidthId, LineHalfWidth);
            PropertyBlock.SetFloat(FaintAlphaId, faintAlpha);

            var position = new Vector3(center.x, AltitudeLayer.MoteLow.AltitudeFor(), center.z);
            Graphics.DrawMesh(mesh, Matrix4x4.TRS(position, Quaternion.identity, Vector3.one), SigilMat, 0, null, 0, PropertyBlock);
        }

        public static void DrawFairies(Vector3 center, FairySigil sigil, int fairyCount, float progress, float alpha = 1f, float radius = FairySigil.WorldRadius)
        {
            if (sigil == null || alpha <= 0f) { return; }

            var lanes = sigil.GetLanes(fairyCount);
            var time = LinkRenderUtility.WrappedAgeSecs;
            for (int lane = 0; lane < lanes.Count; lane++)
            {
                lanes.GetHead(lane, progress, out var local, out var tangent, out var drawing);
                var bob = 0.07f * Mathf.Sin(time * 5.3f + lane * 2.1f);
                var world = FairySigil.ToWorld(center, local, radius);
                DrawFairy(new Vector3(world.x, 0f, world.z + bob), tangent, drawing ? alpha : alpha * 0.8f, lane);
            }
        }

        public static void DrawFairy(Vector3 position, Vector2 heading, float alpha, int seed = 0)
        {
            var glowPos = new Vector3(position.x, AltitudeLayer.MoteOverhead.AltitudeFor(), position.z);
            var flicker = 0.85f + 0.15f * Mathf.Sin(LinkRenderUtility.WrappedAgeSecs * 9f + seed * 1.7f);

            PropertyBlock.Clear();
            PropertyBlock.SetColor(ShaderPropertyIDs.Color, new Color(0.62f * alpha * flicker, 0.5f * alpha * flicker, 0.3f * alpha * flicker, 1f));
            Graphics.DrawMesh(MeshPool.plane10, Matrix4x4.TRS(glowPos, Quaternion.identity, new Vector3(1.2f, 1f, 1.2f)), GlowMat, 0, null, 0, PropertyBlock);

            var twinkle = 0.55f + 0.2f * flicker;
            PropertyBlock.Clear();
            PropertyBlock.SetColor(ShaderPropertyIDs.Color, new Color(alpha, 0.95f * alpha, 0.8f * alpha, 1f));
            Graphics.DrawMesh(MeshPool.plane10, Matrix4x4.TRS(glowPos, Quaternion.identity, new Vector3(twinkle, 1f, twinkle)), TwinkleMat, 0, null, 0, PropertyBlock);

            var rot = Rot4.FromAngleFlat(heading.ToVector3().AngleFlat());
            var fairyGraphic = VVThingDefOf.VV_ViviFairy.graphicData.Graphic;

            var spritePos = glowPos + new Vector3(0f, 0.01f, 0f);
            PropertyBlock.Clear();
            PropertyBlock.SetColor(ShaderPropertyIDs.Color, new Color(1f, 1f, 1f, 0.85f * alpha));
            Graphics.DrawMesh(fairyGraphic.MeshAt(rot), Matrix4x4.TRS(spritePos, Quaternion.identity, Vector3.one), fairyGraphic.MatAt(rot), 0, null, 0, PropertyBlock);
        }

        private static Mesh GetMesh(FairySigilLanes lanes, float radius)
        {
            var key = (lanes, Mathf.RoundToInt(radius * 100f));
            if (MeshCache.TryGetValue(key, out var mesh)) { return mesh; }

            Vertices.Clear();
            Uvs.Clear();
            Colors.Clear();
            Triangles.Clear();

            foreach (var piece in lanes.Pieces)
            {
                var weight = (byte)Mathf.RoundToInt(Mathf.Clamp01(piece.stroke.weight) * 255f);
                Span.Clear();
                piece.stroke.AppendSpan(piece.from, piece.to, Span);

                var startCap = piece.stroke.IsOpenEnd(piece.from);
                var endCap = piece.stroke.IsOpenEnd(piece.to);
                if (startCap)
                {
                    var first = Span[0];
                    var direction = (Span[1].position - first.position).normalized;
                    AddQuad(first.position * radius - direction * LineHalfWidth, first.position * radius, first.offset, first.offset, piece.keyFrom, piece.keyFrom, weight, 0, 255);
                }

                for (int i = 0; i < Span.Count - 1; i++)
                {
                    var a = Span[i];
                    var b = Span[i + 1];
                    AddQuad(a.position * radius, b.position * radius, a.offset, b.offset, piece.KeyAt(a.distance), piece.KeyAt(b.distance), weight, 255, 255);
                }

                if (endCap)
                {
                    var last = Span[Span.Count - 1];
                    var direction = (last.position - Span[Span.Count - 2].position).normalized;
                    AddQuad(last.position * radius, last.position * radius + direction * LineHalfWidth, last.offset, last.offset, piece.keyTo, piece.keyTo, weight, 255, 0);
                }
            }

            mesh = new Mesh { name = "VV_FairySigil_" + lanes.sigil.skill.defName + "_" + lanes.Count };
            mesh.SetVertices(Vertices);
            mesh.SetUVs(0, Uvs);
            mesh.SetColors(Colors);
            mesh.SetTriangles(Triangles, 0);
            mesh.RecalculateBounds();
            MeshCache[key] = mesh;
            return mesh;
        }

        private static void AddQuad(Vector2 a, Vector2 b, Vector2 offsetA, Vector2 offsetB, float keyA, float keyB, byte weight, byte capA, byte capB)
        {
            var index = Vertices.Count;
            var da = offsetA * LineHalfWidth;
            var db = offsetB * LineHalfWidth;

            Vertices.Add(new Vector3(a.x - da.x, 0f, a.y - da.y));
            Vertices.Add(new Vector3(a.x + da.x, 0f, a.y + da.y));
            Vertices.Add(new Vector3(b.x - db.x, 0f, b.y - db.y));
            Vertices.Add(new Vector3(b.x + db.x, 0f, b.y + db.y));

            Uvs.Add(new Vector2(keyA, 0f));
            Uvs.Add(new Vector2(keyA, 1f));
            Uvs.Add(new Vector2(keyB, 0f));
            Uvs.Add(new Vector2(keyB, 1f));

            Colors.Add(new Color32(weight, weight, weight, capA));
            Colors.Add(new Color32(weight, weight, weight, capA));
            Colors.Add(new Color32(weight, weight, weight, capB));
            Colors.Add(new Color32(weight, weight, weight, capB));

            Triangles.Add(index);
            Triangles.Add(index + 1);
            Triangles.Add(index + 3);
            Triangles.Add(index);
            Triangles.Add(index + 3);
            Triangles.Add(index + 2);
        }
    }
}
