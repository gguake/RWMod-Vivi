using RimWorld;
using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace VVRace
{
    [StaticConstructorOnStartup]
    public static class RuinHexFieldRenderer
    {
        public const int MaxVertices = 6;
        public const float EdgeMargin = 0.7f;

        private const string ShaderPath = "Map/VV_RuinHexField";

        private static readonly Material FieldMat;
        private static readonly MaterialPropertyBlock PropertyBlock = new MaterialPropertyBlock();

        private static readonly int SizeXId = Shader.PropertyToID("_SizeX");
        private static readonly int SizeYId = Shader.PropertyToID("_SizeY");
        private static readonly int OriginXId = Shader.PropertyToID("_OriginX");
        private static readonly int OriginYId = Shader.PropertyToID("_OriginY");
        private static readonly int CountId = Shader.PropertyToID("_Count");
        private static readonly int WhitenId = Shader.PropertyToID("_Whiten");
        private static readonly int[] PointXIds = new int[MaxVertices];
        private static readonly int[] PointYIds = new int[MaxVertices];

        static RuinHexFieldRenderer()
        {
            var shader = ShaderDatabase.LoadShader(ShaderPath);
            if (shader != null)
            {
                FieldMat = MaterialPool.MatFrom(BaseContent.WhiteTex, shader, Color.white);
            }

            for (int i = 0; i < MaxVertices; i++)
            {
                PointXIds[i] = Shader.PropertyToID("_P" + i + "X");
                PointYIds[i] = Shader.PropertyToID("_P" + i + "Y");
            }
        }

        public static void Draw(IReadOnlyList<Vector3> points, Color color, float alpha = 1f, float whiten = 0f)
        {
            if (FieldMat == null || points == null) { return; }

            var count = Mathf.Min(points.Count, MaxVertices);
            if (alpha <= 0.001f || count < 3) { return; }

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

            var tint = color;
            tint.a *= alpha;

            PropertyBlock.Clear();
            PropertyBlock.SetColor(ShaderPropertyIDs.Color, tint);
            PropertyBlock.SetFloat(ShaderPropertyIDs.AgeSecs, LinkRenderUtility.WrappedAgeSecs);
            PropertyBlock.SetFloat(SizeXId, sizeX);
            PropertyBlock.SetFloat(SizeYId, sizeZ);
            PropertyBlock.SetFloat(OriginXId, minX);
            PropertyBlock.SetFloat(OriginYId, minZ);
            PropertyBlock.SetFloat(CountId, count);
            PropertyBlock.SetFloat(WhitenId, Mathf.Clamp01(whiten));
            for (int i = 0; i < count; i++)
            {
                PropertyBlock.SetFloat(PointXIds[i], points[i].x - minX);
                PropertyBlock.SetFloat(PointYIds[i], points[i].z - minZ);
            }

            var position = new Vector3(minX + sizeX * 0.5f, AltitudeLayer.MoteLow.AltitudeFor(), minZ + sizeZ * 0.5f);
            Graphics.DrawMesh(MeshPool.plane10, Matrix4x4.TRS(position, Quaternion.identity, new Vector3(sizeX, 1f, sizeZ)), FieldMat, 0, null, 0, PropertyBlock);
        }
    }
}
