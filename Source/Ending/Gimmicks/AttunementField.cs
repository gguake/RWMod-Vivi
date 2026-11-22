using RimWorld;
using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace VVRace
{
    [StaticConstructorOnStartup]
    public static class AttunementField
    {
        public const int MaxVertices = 6;
        public const float EdgeMargin = 0.7f;

        public static readonly Color DefaultColor = new Color(1f, 0.82f, 0.4f, 1f);

        private static readonly Material FieldMat = MaterialPool.MatFrom(BaseContent.WhiteTex, ShaderDatabase.LoadShader("Map/VV_AttunementField"), Color.white);
        private static readonly MaterialPropertyBlock PropertyBlock = new MaterialPropertyBlock();

        private static readonly int SizeXId = Shader.PropertyToID("_SizeX");
        private static readonly int SizeYId = Shader.PropertyToID("_SizeY");
        private static readonly int OriginXId = Shader.PropertyToID("_OriginX");
        private static readonly int OriginYId = Shader.PropertyToID("_OriginY");
        private static readonly int CountId = Shader.PropertyToID("_Count");
        private static readonly int WhitenId = Shader.PropertyToID("_Whiten");
        private static readonly int[] PointXIds = new int[MaxVertices];
        private static readonly int[] PointYIds = new int[MaxVertices];

        static AttunementField()
        {
            for (int i = 0; i < MaxVertices; i++)
            {
                PointXIds[i] = Shader.PropertyToID("_P" + i + "X");
                PointYIds[i] = Shader.PropertyToID("_P" + i + "Y");
            }
        }

        public static void Draw(List<Vector3> points, float alpha = 1f, float whiten = 0f)
        {
            var count = Mathf.Min(points.Count, MaxVertices);
            if (alpha <= 0f || count < 3) { return; }

            var minX = float.MaxValue;
            var minZ = float.MaxValue;
            var maxX = float.MinValue;
            var maxZ = float.MinValue;
            for (int i = 0; i < count; i++)
            {
                minX = Mathf.Min(minX, points[i].x);
                minZ = Mathf.Min(minZ, points[i].z);
                maxX = Mathf.Max(maxX, points[i].x);
                maxZ = Mathf.Max(maxZ, points[i].z);
            }

            minX -= EdgeMargin;
            minZ -= EdgeMargin;
            var sizeX = maxX + EdgeMargin - minX;
            var sizeZ = maxZ + EdgeMargin - minZ;

            var tint = DefaultColor;
            tint.a *= alpha;

            PropertyBlock.Clear();
            PropertyBlock.SetColor(ShaderPropertyIDs.Color, tint);
            PropertyBlock.SetFloat(ShaderPropertyIDs.AgeSecs, LinkRenderUtility.WrappedAgeSecs);
            PropertyBlock.SetFloat(SizeXId, sizeX);
            PropertyBlock.SetFloat(SizeYId, sizeZ);
            PropertyBlock.SetFloat(OriginXId, minX);
            PropertyBlock.SetFloat(OriginYId, minZ);
            PropertyBlock.SetFloat(CountId, count);
            PropertyBlock.SetFloat(WhitenId, whiten);
            for (int i = 0; i < count; i++)
            {
                PropertyBlock.SetFloat(PointXIds[i], points[i].x - minX);
                PropertyBlock.SetFloat(PointYIds[i], points[i].z - minZ);
            }

            var position = new Vector3(minX + sizeX * 0.5f, AltitudeLayer.MoteLow.AltitudeFor(), minZ + sizeZ * 0.5f);
            Graphics.DrawMesh(MeshPool.plane10, Matrix4x4.TRS(position, Quaternion.identity, new Vector3(sizeX, 1f, sizeZ)), FieldMat, 0, null, 0, PropertyBlock);
        }

        public static float PolygonArea(List<Vector3> points)
        {
            var sum = 0f;
            for (int i = 0, j = points.Count - 1; i < points.Count; j = i, i++)
            {
                sum += points[j].ToVector2().Cross(points[i].ToVector2());
            }
            return Mathf.Abs(sum) * 0.5f;
        }

        public static bool PointInPolygon(Vector3 p, List<Vector3> points)
        {
            var inside = false;
            for (int i = 0, j = points.Count - 1; i < points.Count; j = i, i++)
            {
                var a = points[i];
                var b = points[j];
                if ((a.z > p.z) != (b.z > p.z) && p.x < (b.x - a.x) * (p.z - a.z) / (b.z - a.z) + a.x)
                {
                    inside = !inside;
                }
            }
            return inside;
        }

        public static bool CircleIntersectsSegment(Vector3 center, float radius, Vector3 a, Vector3 b)
        {
            var p = center.ToVector2();
            var start = a.ToVector2();
            var ab = b.ToVector2() - start;
            var lengthSq = ab.sqrMagnitude;
            var t = lengthSq <= 1e-12f ? 0f : Mathf.Clamp01(Vector2.Dot(p - start, ab) / lengthSq);
            return Vector2.Distance(p, start + ab * t) <= radius;
        }
    }
}
