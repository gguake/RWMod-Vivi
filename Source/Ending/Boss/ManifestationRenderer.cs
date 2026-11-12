using RimWorld;
using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace VVRace
{
    [StaticConstructorOnStartup]
    internal static class ManifestationRenderer
    {
        private const string ShaderPath = "Map/VV_Manifest";

        private static readonly int ProgressId = Shader.PropertyToID("_Progress");
        private static readonly int RevealId = Shader.PropertyToID("_Reveal");
        private static readonly MaterialPropertyBlock PropertyBlock = new MaterialPropertyBlock();
        private static readonly Dictionary<string, Material> MaterialCache = new Dictionary<string, Material>();

        public static void Draw(string texPath, Rot4 rotation, Vector3 pos, float scale, float ageSecs, float progress, float reveal)
        {
            var material = DissolveRenderer.GetNoiseMaterial(texPath + DissolveRenderer.RotationSuffix(rotation), ShaderPath, MaterialCache, noiseFirst: true);
            if (material == null) { return; }

            PropertyBlock.Clear();
            PropertyBlock.SetFloat(ShaderPropertyIDs.AgeSecs, ageSecs);
            PropertyBlock.SetFloat(ProgressId, progress);
            PropertyBlock.SetFloat(RevealId, reveal);

            var mesh = rotation == Rot4.West ? MeshPool.plane10Flip : MeshPool.plane10;
            var matrix = Matrix4x4.TRS(pos, Quaternion.identity, new Vector3(scale, 1f, scale));
            Graphics.DrawMesh(mesh, matrix, material, 0, null, 0, PropertyBlock);
        }

        public static void ThrowConverging(Map map, FleckDef def, Vector3 start, Vector3 target, float travelSecs, float scale, Color? color, bool alignToVelocity)
        {
            var travel = (target - start).Yto0();
            var data = FleckMaker.GetDataStatic(start, map, def, scale);
            data.velocityAngle = travel.AngleFlat();
            data.velocitySpeed = travel.magnitude / travelSecs;
            data.rotation = alignToVelocity ? data.velocityAngle : Rand.Range(0f, 360f);
            if (color.HasValue)
            {
                data.instanceColor = color;
            }
            map.flecks.CreateFleck(data);
        }
    }
}
