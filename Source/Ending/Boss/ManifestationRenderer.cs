using RimWorld;
using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace VVRace
{
    // 형체를 빚는 연출이 공유하는 재질과 그리기. 위치와 진행도는 각 연출에서 계산한다.
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

        // 시작점에서 목표점까지 travelSecs 동안 날아가 모이는 플렉을 만든다.
        // 크기 난수는 호출부 인자에서 먼저 소비되고, 회전 난수(정렬하지 않을 때)는 여기서 소비된다.
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
