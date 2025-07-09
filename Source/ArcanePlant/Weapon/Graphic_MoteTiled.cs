using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using Verse;

namespace VVRace
{
    public class Graphic_MoteTiled : Graphic_Single
    {
        protected static MaterialPropertyBlock propertyBlock = new MaterialPropertyBlock();

        protected static Dictionary<int, Mesh> _meshes = new Dictionary<int, Mesh>();

        public override void DrawWorker(Vector3 loc, Rot4 rot, ThingDef thingDef, Thing thing, float extraRotation)
        {
            DrawMoteInternal(loc, rot, thingDef, thing, 0);
        }

        public void DrawMoteInternal(Vector3 loc, Rot4 rot, ThingDef thingDef, Thing thing, int layer)
        {
            DrawMote(data, MatSingle, base.Color, loc, rot, thingDef, thing, 0);
        }

        public static void DrawMote(GraphicData data, Material material, Color color, Vector3 loc, Rot4 rot, ThingDef thingDef, Thing thing, int layer)
        {
            var mote = (Mote)thing;
            var alpha = mote.Alpha;
            if (thing != null && alpha > 0f)
            {
                var multipliedColor = color * mote.instanceColor;
                multipliedColor.a *= alpha;

                var exactScale = mote.ExactScale;
                exactScale.x *= data.drawSize.x;
                exactScale.z *= data.drawSize.y;

                var mesh = MakeUVScaledMesh(Mathf.Max(1, (int)(exactScale.z * 10)));

                Matrix4x4 matrix = default;
                matrix.SetTRS(mote.DrawPos, Quaternion.AngleAxis(mote.exactRotation, Vector3.up), exactScale);

                material.mainTexture.wrapModeV = TextureWrapMode.Repeat;

                propertyBlock.SetFloat(ShaderPropertyIDs.AgeSecs, mote.AgeSecs);
                propertyBlock.SetFloat(ShaderPropertyIDs.AgeSecsPausable, mote.AgeSecsPausable);
                propertyBlock.SetFloat(ShaderPropertyIDs.RandomPerObject, Gen.HashCombineInt(mote.spawnTick, mote.DrawPos.GetHashCode()));
                propertyBlock.SetFloat(ShaderPropertyIDs.RandomPerObjectOffsetRandom, Gen.HashCombineInt(mote.spawnTick, mote.offsetRandom));
                propertyBlock.SetColor(ShaderPropertyIDs.Color, multipliedColor);
                Graphics.DrawMesh(mesh, matrix, material, layer, null, 0, propertyBlock);
            }
        }

        private static Mesh MakeUVScaledMesh(int yScale)
        {
            if (_meshes.TryGetValue(yScale, out var mesh)) { return mesh; }

            var actualScale = yScale / 20f;
            var vtx = new Vector3[4];
            var uvs = new Vector2[4];

            var idx = new int[6];
            vtx[0] = new Vector3(-0.5f, 0f, -0.5f);
            vtx[1] = new Vector3(-0.5f, 0f, 0.5f);
            vtx[2] = new Vector3(0.5f, 0f, 0.5f);
            vtx[3] = new Vector3(0.5f, 0f, -0.5f);

            uvs[0] = new Vector2(1f, 0f);
            uvs[1] = new Vector2(1f, actualScale);
            uvs[2] = new Vector2(0f, actualScale);
            uvs[3] = new Vector2(0f, 0f);

            idx[0] = 0;
            idx[1] = 1;
            idx[2] = 2;
            idx[3] = 0;
            idx[4] = 2;
            idx[5] = 3;

            mesh = new Mesh();
            mesh.name = "NewPlaneMesh()";
            mesh.vertices = vtx;
            mesh.uv = uvs;
            mesh.SetTriangles(idx, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            _meshes.Add(yScale, mesh);
            return mesh;
        }
    }
}
