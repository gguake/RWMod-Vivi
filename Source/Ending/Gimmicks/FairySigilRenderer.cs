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
        private static readonly Dictionary<SigilStroke, StrokeRibbon> RibbonScratch = new Dictionary<SigilStroke, StrokeRibbon>();

        private const float SplitEpsilon = 1e-5f;

        private struct CornerFan
        {
            public bool enabled;
            public Vector2 center;
            public float startAngle;
            public float deltaAngle;
            public int steps;
        }

        private sealed class StrokeRibbon
        {
            public readonly int segCount;
            public readonly Vector2[] pStart;
            public readonly Vector2[] pEnd;
            public readonly Vector2[] dir;
            public readonly Vector2[] norm;
            public readonly Vector2[] leftStart;
            public readonly Vector2[] leftEnd;
            public readonly Vector2[] rightStart;
            public readonly Vector2[] rightEnd;
            public readonly float[] dlStart;
            public readonly float[] dlEnd;
            public readonly float[] drStart;
            public readonly float[] drEnd;
            public readonly CornerFan[] fans;

            public StrokeRibbon(SigilStroke stroke, float radius, float halfWidth)
            {
                var path = stroke.Path;
                segCount = path.Length - 1;
                pStart = new Vector2[segCount];
                pEnd = new Vector2[segCount];
                dir = new Vector2[segCount];
                norm = new Vector2[segCount];
                leftStart = new Vector2[segCount];
                leftEnd = new Vector2[segCount];
                rightStart = new Vector2[segCount];
                rightEnd = new Vector2[segCount];
                dlStart = new float[segCount];
                dlEnd = new float[segCount];
                drStart = new float[segCount];
                drEnd = new float[segCount];
                fans = new CornerFan[segCount];

                var segLens = new float[segCount];
                for (int i = 0; i < segCount; i++)
                {
                    var a = path[i] * radius;
                    var b = path[i + 1] * radius;
                    var v = b - a;
                    var len = v.magnitude;
                    var d = len > 1e-9f ? v / len : Vector2.right;
                    var n = new Vector2(-d.y, d.x);

                    pStart[i] = a;
                    pEnd[i] = b;
                    dir[i] = d;
                    norm[i] = n;
                    segLens[i] = len;

                    leftStart[i] = a + n * halfWidth;
                    leftEnd[i] = b + n * halfWidth;
                    rightStart[i] = a - n * halfWidth;
                    rightEnd[i] = b - n * halfWidth;
                    dlStart[i] = 1f;
                    dlEnd[i] = 1f;
                    drStart[i] = 1f;
                    drEnd[i] = 1f;
                }

                int firstJoin = stroke.closed ? 0 : 1;
                for (int j = firstJoin; j < segCount; j++)
                {
                    int iIn = (j - 1 + segCount) % segCount;
                    int iOut = j;
                    var vtx = pStart[iOut];
                    var dIn = dir[iIn];
                    var dOut = dir[iOut];
                    var nIn = norm[iIn];
                    var nOut = norm[iOut];

                    var dot = Mathf.Clamp(Vector2.Dot(dIn, dOut), -1f, 1f);
                    var cross = dIn.x * dOut.y - dIn.y * dOut.x;
                    var turn = Mathf.Acos(dot);
                    if (turn < 1e-4f) { continue; }

                    var halfTurn = turn * 0.5f;
                    var cosH = Mathf.Cos(halfTurn);
                    var sinH = Mathf.Sin(halfTurn);
                    var maxSegHalf = 0.48f * Mathf.Min(segLens[iIn], segLens[iOut]);
                    var dist = Mathf.Min(halfWidth / Mathf.Max(cosH, 1e-4f), maxSegHalf / Mathf.Max(sinH, 1e-4f));
                    var truePerpNorm = Mathf.Clamp01((dist * cosH) / halfWidth);

                    var bis = nIn + nOut;
                    bis = bis.sqrMagnitude < 1e-12f ? (cross >= 0f ? -dIn : dIn) : bis.normalized;

                    if (cross > 0f)
                    {
                        var innerPt = vtx + bis * dist;
                        leftEnd[iIn] = innerPt;
                        leftStart[iOut] = innerPt;
                        dlEnd[iIn] = truePerpNorm;
                        dlStart[iOut] = truePerpNorm;

                        if (cosH >= 0.92f && truePerpNorm >= 0.99f)
                        {
                            var outerPt = vtx - bis * (halfWidth / cosH);
                            rightEnd[iIn] = outerPt;
                            rightStart[iOut] = outerPt;
                        }
                        else
                        {
                            fans[j] = MakeFan(vtx, -nIn, -nOut, turn);
                        }
                    }
                    else
                    {
                        var innerPt = vtx - bis * dist;
                        rightEnd[iIn] = innerPt;
                        rightStart[iOut] = innerPt;
                        drEnd[iIn] = truePerpNorm;
                        drStart[iOut] = truePerpNorm;

                        if (cosH >= 0.92f && truePerpNorm >= 0.99f)
                        {
                            var outerPt = vtx + bis * (halfWidth / cosH);
                            leftEnd[iIn] = outerPt;
                            leftStart[iOut] = outerPt;
                        }
                        else
                        {
                            fans[j] = MakeFan(vtx, nIn, nOut, turn);
                        }
                    }
                }
            }

            private static CornerFan MakeFan(Vector2 center, Vector2 fromNorm, Vector2 toNorm, float turn)
            {
                var a0 = Mathf.Atan2(fromNorm.y, fromNorm.x);
                var a1 = Mathf.Atan2(toNorm.y, toNorm.x);
                var da = Mathf.DeltaAngle(a0 * Mathf.Rad2Deg, a1 * Mathf.Rad2Deg) * Mathf.Deg2Rad;
                var steps = Mathf.Max(2, Mathf.CeilToInt(turn / (Mathf.PI / 6f)));
                return new CornerFan
                {
                    enabled = true,
                    center = center,
                    startAngle = a0,
                    deltaAngle = da,
                    steps = steps,
                };
            }
        }

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
            RibbonScratch.Clear();

            foreach (var piece in lanes.Pieces)
            {
                var stroke = piece.stroke;
                if (!RibbonScratch.TryGetValue(stroke, out var ribbon))
                {
                    ribbon = new StrokeRibbon(stroke, radius, LineHalfWidth);
                    RibbonScratch[stroke] = ribbon;
                }

                var weight = (byte)Mathf.RoundToInt(Mathf.Clamp01(stroke.weight) * 255f);
                var cum = stroke.Cumulative;
                var lo = Mathf.Min(piece.from, piece.to);
                var hi = Mathf.Max(piece.from, piece.to);

                for (int i = 0; i < ribbon.segCount; i++)
                {
                    var segStart = cum[i];
                    var segEnd = cum[i + 1];
                    if (segEnd <= lo + SplitEpsilon || segStart >= hi - SplitEpsilon) { continue; }

                    var segSpan = segEnd - segStart;
                    if (segSpan <= 1e-9f) { continue; }

                    var cLo = Mathf.Max(lo, segStart);
                    var cHi = Mathf.Min(hi, segEnd);
                    var f0 = (cLo - segStart) / segSpan;
                    var f1 = (cHi - segStart) / segSpan;

                    var p0 = Vector2.LerpUnclamped(ribbon.pStart[i], ribbon.pEnd[i], f0);
                    var p1 = Vector2.LerpUnclamped(ribbon.pStart[i], ribbon.pEnd[i], f1);
                    var ls = Vector2.LerpUnclamped(ribbon.leftStart[i], ribbon.leftEnd[i], f0);
                    var le = Vector2.LerpUnclamped(ribbon.leftStart[i], ribbon.leftEnd[i], f1);
                    var rs = Vector2.LerpUnclamped(ribbon.rightStart[i], ribbon.rightEnd[i], f0);
                    var re = Vector2.LerpUnclamped(ribbon.rightStart[i], ribbon.rightEnd[i], f1);
                    var dls = Mathf.LerpUnclamped(ribbon.dlStart[i], ribbon.dlEnd[i], f0);
                    var dle = Mathf.LerpUnclamped(ribbon.dlStart[i], ribbon.dlEnd[i], f1);
                    var drs = Mathf.LerpUnclamped(ribbon.drStart[i], ribbon.drEnd[i], f0);
                    var dre = Mathf.LerpUnclamped(ribbon.drStart[i], ribbon.drEnd[i], f1);

                    var k0 = piece.KeyAt(cLo);
                    var k1 = piece.KeyAt(cHi);

                    // 중심선 기준으로 좌/우 반쪽 쿼드를 나눠 안쪽 클램프 거리가 정확히 선형 보간되도록 한다.
                    AddStripQuad(p0, ls, le, p1, k0, k0, k1, k1, 0f, dls, dle, 0f, weight, 0, 0, 0, 0);
                    AddStripQuad(p0, p1, re, rs, k0, k1, k1, k0, 0f, 0f, dre, drs, weight, 0, 0, 0, 0);
                }

                // 예각/곡률 큰 꼭짓점 바깥쪽은 둥근 부채꼴로 1회만 채워 스파이크와 중복을 모두 없앤다.
                int firstJoin = stroke.closed ? 0 : 1;
                for (int j = firstJoin; j < ribbon.segCount; j++)
                {
                    var fan = ribbon.fans[j];
                    if (!fan.enabled) { continue; }
                    var distAtCorner = cum[j];
                    if (distAtCorner < lo - SplitEpsilon || distAtCorner >= hi - SplitEpsilon) { continue; }

                    var cornerKey = piece.KeyAt(Mathf.Clamp(distAtCorner, lo, hi));
                    var prevRim = fan.center + new Vector2(Mathf.Cos(fan.startAngle), Mathf.Sin(fan.startAngle)) * LineHalfWidth;
                    for (int s = 1; s <= fan.steps; s++)
                    {
                        var ang = fan.startAngle + fan.deltaAngle * (s / (float)fan.steps);
                        var curRim = fan.center + new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * LineHalfWidth;
                        AddTri(fan.center, prevRim, curRim, cornerKey, cornerKey, cornerKey, 0f, 1f, 1f, weight, 0, 0, 0);
                        prevRim = curRim;
                    }
                }

                // 열린 끝점은 vColor.g(길이 방향 연장 거리)로 정확한 원형 캡을 그려 인접 선 침범을 막는다.
                if (!stroke.closed)
                {
                    if (lo <= SplitEpsilon)
                    {
                        AddRoundCap(ribbon.pStart[0], -ribbon.dir[0], ribbon.norm[0], piece.KeyAt(0f), weight);
                    }
                    if (hi >= stroke.Length - SplitEpsilon)
                    {
                        var last = ribbon.segCount - 1;
                        AddRoundCap(ribbon.pEnd[last], ribbon.dir[last], ribbon.norm[last], piece.KeyAt(stroke.Length), weight);
                    }
                }
            }

            RibbonScratch.Clear();

            mesh = new Mesh { name = "VV_FairySigil_" + lanes.sigil.name + "_" + lanes.Count };
            mesh.SetVertices(Vertices);
            mesh.SetUVs(0, Uvs);
            mesh.SetColors(Colors);
            mesh.SetTriangles(Triangles, 0);
            mesh.RecalculateBounds();
            MeshCache[key] = mesh;
            return mesh;
        }

        private static void AddRoundCap(Vector2 endPoint, Vector2 outwardDir, Vector2 normal, float key, byte weight)
        {
            var tip = endPoint + outwardDir * LineHalfWidth;
            var n = normal * LineHalfWidth;
            AddStripQuad(endPoint, endPoint + n, tip + n, tip, key, key, key, key, 0f, 1f, 1f, 0f, weight, 0, 0, 255, 255);
            AddStripQuad(endPoint, tip, tip - n, endPoint - n, key, key, key, key, 0f, 0f, 1f, 1f, weight, 0, 255, 255, 0);
        }

        private static void AddStripQuad(
            Vector2 p0, Vector2 p1, Vector2 p2, Vector2 p3,
            float k0, float k1, float k2, float k3,
            float d0, float d1, float d2, float d3,
            byte weight, byte al0, byte al1, byte al2, byte al3)
        {
            var index = Vertices.Count;

            Vertices.Add(new Vector3(p0.x, 0f, p0.y));
            Vertices.Add(new Vector3(p1.x, 0f, p1.y));
            Vertices.Add(new Vector3(p2.x, 0f, p2.y));
            Vertices.Add(new Vector3(p3.x, 0f, p3.y));

            Uvs.Add(new Vector2(k0, d0));
            Uvs.Add(new Vector2(k1, d1));
            Uvs.Add(new Vector2(k2, d2));
            Uvs.Add(new Vector2(k3, d3));

            Colors.Add(new Color32(weight, al0, 0, 255));
            Colors.Add(new Color32(weight, al1, 0, 255));
            Colors.Add(new Color32(weight, al2, 0, 255));
            Colors.Add(new Color32(weight, al3, 0, 255));

            Triangles.Add(index);
            Triangles.Add(index + 1);
            Triangles.Add(index + 2);
            Triangles.Add(index);
            Triangles.Add(index + 2);
            Triangles.Add(index + 3);
        }

        private static void AddTri(
            Vector2 p0, Vector2 p1, Vector2 p2,
            float k0, float k1, float k2,
            float d0, float d1, float d2,
            byte weight, byte al0, byte al1, byte al2)
        {
            var index = Vertices.Count;

            Vertices.Add(new Vector3(p0.x, 0f, p0.y));
            Vertices.Add(new Vector3(p1.x, 0f, p1.y));
            Vertices.Add(new Vector3(p2.x, 0f, p2.y));

            Uvs.Add(new Vector2(k0, d0));
            Uvs.Add(new Vector2(k1, d1));
            Uvs.Add(new Vector2(k2, d2));

            Colors.Add(new Color32(weight, al0, 0, 255));
            Colors.Add(new Color32(weight, al1, 0, 255));
            Colors.Add(new Color32(weight, al2, 0, 255));

            Triangles.Add(index);
            Triangles.Add(index + 1);
            Triangles.Add(index + 2);
        }
    }
}
