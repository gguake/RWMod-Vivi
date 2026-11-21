using RimWorld;
using UnityEngine;
using Verse;

namespace VVRace
{
    [StaticConstructorOnStartup]
    public static class AttunementField
    {
        public const float EdgeMargin = 0.7f;

        public static readonly Color DefaultColor = new Color(1f, 0.82f, 0.4f, 1f);

        private static readonly Material FieldMat = MaterialPool.MatFrom(BaseContent.WhiteTex, ShaderDatabase.LoadShader("Map/VV_AttunementField"), Color.white);
        private static readonly MaterialPropertyBlock PropertyBlock = new MaterialPropertyBlock();

        private static readonly int SizeXId = Shader.PropertyToID("_SizeX");
        private static readonly int SizeYId = Shader.PropertyToID("_SizeY");
        private static readonly int OriginXId = Shader.PropertyToID("_OriginX");
        private static readonly int OriginYId = Shader.PropertyToID("_OriginY");
        private static readonly int AxId = Shader.PropertyToID("_AX");
        private static readonly int AyId = Shader.PropertyToID("_AY");
        private static readonly int BxId = Shader.PropertyToID("_BX");
        private static readonly int ById = Shader.PropertyToID("_BY");
        private static readonly int CxId = Shader.PropertyToID("_CX");
        private static readonly int CyId = Shader.PropertyToID("_CY");

        public static void Draw(Vector3 a, Vector3 b, Vector3 c, float alpha = 1f, Color? color = null)
        {
            if (alpha <= 0f) { return; }

            var minX = Mathf.Min(a.x, Mathf.Min(b.x, c.x)) - EdgeMargin;
            var minZ = Mathf.Min(a.z, Mathf.Min(b.z, c.z)) - EdgeMargin;
            var sizeX = Mathf.Max(a.x, Mathf.Max(b.x, c.x)) + EdgeMargin - minX;
            var sizeZ = Mathf.Max(a.z, Mathf.Max(b.z, c.z)) + EdgeMargin - minZ;

            var tint = color ?? DefaultColor;
            tint.a *= alpha;

            PropertyBlock.Clear();
            PropertyBlock.SetColor(ShaderPropertyIDs.Color, tint);
            PropertyBlock.SetFloat(ShaderPropertyIDs.AgeSecs, LinkRenderUtility.WrappedAgeSecs);
            PropertyBlock.SetFloat(SizeXId, sizeX);
            PropertyBlock.SetFloat(SizeYId, sizeZ);
            PropertyBlock.SetFloat(OriginXId, minX);
            PropertyBlock.SetFloat(OriginYId, minZ);
            PropertyBlock.SetFloat(AxId, a.x - minX);
            PropertyBlock.SetFloat(AyId, a.z - minZ);
            PropertyBlock.SetFloat(BxId, b.x - minX);
            PropertyBlock.SetFloat(ById, b.z - minZ);
            PropertyBlock.SetFloat(CxId, c.x - minX);
            PropertyBlock.SetFloat(CyId, c.z - minZ);

            var position = new Vector3(minX + sizeX * 0.5f, AltitudeLayer.MoteLow.AltitudeFor(), minZ + sizeZ * 0.5f);
            Graphics.DrawMesh(MeshPool.plane10, Matrix4x4.TRS(position, Quaternion.identity, new Vector3(sizeX, 1f, sizeZ)), FieldMat, 0, null, 0, PropertyBlock);
        }

        public static float TriangleArea(Vector3 a, Vector3 b, Vector3 c)
        {
            return Mathf.Abs((b - a).ToVector2().Cross((c - a).ToVector2())) * 0.5f;
        }

        public static bool PointInTriangle(Vector3 p, Vector3 a, Vector3 b, Vector3 c)
        {
            var d1 = (b - a).ToVector2().Cross((p - a).ToVector2());
            var d2 = (c - b).ToVector2().Cross((p - b).ToVector2());
            var d3 = (a - c).ToVector2().Cross((p - c).ToVector2());
            var hasNegative = d1 < 0f || d2 < 0f || d3 < 0f;
            var hasPositive = d1 > 0f || d2 > 0f || d3 > 0f;
            return !(hasNegative && hasPositive);
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
