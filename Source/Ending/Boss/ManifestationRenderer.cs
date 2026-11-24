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
        private const string FbmNoiseTexPath = "VFX/VV_TileableFbmNoise";

        private static readonly int ProgressId = Shader.PropertyToID("_Progress");
        private static readonly int RevealId = Shader.PropertyToID("_Reveal");
        private static readonly int EdgeColorId = Shader.PropertyToID("_EdgeColor");
        private static readonly int DarkColorId = Shader.PropertyToID("_DarkColor");
        private static readonly int SoftId = Shader.PropertyToID("_Soft");
        private static readonly MaterialPropertyBlock PropertyBlock = new MaterialPropertyBlock();
        private static readonly Dictionary<string, Material> MaterialCache = new Dictionary<string, Material>();
        private static readonly Texture2D FbmNoiseTex = ContentFinder<Texture2D>.Get(FbmNoiseTexPath, reportFailure: false);

        public static void Draw(string texPath, Rot4 rotation, Vector3 pos, float scale, float ageSecs, float progress, float reveal, Color? edgeColor = null, Color? bodyColor = null, float soft = 0f)
        {
            var key = texPath + DissolveRenderer.RotationSuffix(rotation);
            var hadCached = MaterialCache.ContainsKey(key);
            var material = DissolveRenderer.GetNoiseMaterial(key, ShaderPath, MaterialCache);
            if (material == null) { return; }
            if (!hadCached && FbmNoiseTex != null)
            {
                material.SetTexture("_NoiseTex", FbmNoiseTex);
            }

            PropertyBlock.Clear();
            PropertyBlock.SetFloat(ShaderPropertyIDs.AgeSecs, ageSecs);
            PropertyBlock.SetFloat(ProgressId, progress);
            PropertyBlock.SetFloat(RevealId, reveal);
            PropertyBlock.SetFloat(SoftId, soft);
            if (edgeColor.HasValue)
            {
                PropertyBlock.SetColor(EdgeColorId, edgeColor.Value);
            }
            if (bodyColor.HasValue)
            {
                PropertyBlock.SetColor(DarkColorId, bodyColor.Value);
            }

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

        public static void ThrowWispWithSpark(
            Map map,
            Vector3 start,
            Vector3 target,
            float travelSecs,
            FloatRange wispScale,
            float sparkChance,
            FloatRange sparkScale,
            Color sparkColor)
        {
            ThrowConverging(map, VVFleckDefOf.VV_Fleck_ApocryphaDarkWisp, start, target, travelSecs, wispScale.RandomInRange, null, alignToVelocity: true);

            if (Rand.Chance(sparkChance))
            {
                ThrowConverging(map, VVFleckDefOf.VV_Fleck_ApocryphaManifestSpark, start, target, travelSecs, sparkScale.RandomInRange, sparkColor, alignToVelocity: false);
            }
        }
    }
}
