using RimWorld;
using UnityEngine;
using Verse;

namespace VVRace
{
    // 동화 공명자는 본체를 직접 그리지 않고 바닐라 초상화 캐시 텍스처를 판 한 장으로 띄운다.
    // 초상화 경로는 애니메이션과 자세 변형을 건너뛰므로 다른 모드가 몸이나 머리를 기울이거나 눕히지 못한다.
    [StaticConstructorOnStartup]
    internal static class AssimilatedResonatorRenderer
    {
        // 폰을 중심으로 텍스처가 덮는 범위(칸). 바닐라의 축소 화면용 캐시는 2칸이고, 큰 머리 장식을 위해 여유를 둔다.
        private const float QuadSize = 2.5f;
        private const float CameraZoom = 2f / QuadSize;

        private static readonly Vector2[] TextureSizes =
        {
            new Vector2(128f, 128f),
            new Vector2(256f, 256f),
            new Vector2(512f, 512f),
        };

        private static readonly Material QuadMat = new Material(ShaderDatabase.Cutout) { name = "VV_AssimilatedResonator", color = Color.white };
        private static readonly MaterialPropertyBlock PropertyBlock = new MaterialPropertyBlock();
        private static readonly int MainTexPropertyId = Shader.PropertyToID("_MainTex");

        public static void Draw(Pawn pawn, Vector3 drawPos)
        {
            // 텍스처는 바닐라가 만료시키고 다른 폰에 재사용하므로 보관하지 않고 그릴 때마다 받는다.
            // 쓰러진 폰은 초상화에서 눕혀 그리므로 건강 상태를 정상으로 덮어쓴다.
            var texture = PortraitsCache.Get(
                pawn,
                TextureSizeForCurrentZoom(),
                Rot4.South,
                cameraZoom: CameraZoom,
                supersample: false,
                compensateForUIScale: false,
                healthStateOverride: PawnHealthState.Mobile);

            PropertyBlock.Clear();
            PropertyBlock.SetTexture(MainTexPropertyId, texture);

            var matrix = Matrix4x4.TRS(drawPos, Quaternion.identity, new Vector3(QuadSize, 1f, QuadSize));
            Graphics.DrawMesh(MeshPool.plane10, matrix, QuadMat, 0, null, 0, PropertyBlock);
        }

        // 밉맵이 없는 텍스처라 화면에 그려지는 크기보다 많이 크면 자글거린다. 화면 크기 이상인 가장 작은 단계를 고른다.
        private static Vector2 TextureSizeForCurrentZoom()
        {
            var camera = Find.Camera;
            var screenPixels = QuadSize * camera.pixelHeight / (2f * camera.orthographicSize);

            for (int i = 0; i < TextureSizes.Length - 1; i++)
            {
                if (TextureSizes[i].x >= screenPixels)
                {
                    return TextureSizes[i];
                }
            }

            return TextureSizes[TextureSizes.Length - 1];
        }
    }
}
