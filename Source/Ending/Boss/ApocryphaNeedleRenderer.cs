using RimWorld;
using UnityEngine;
using Verse;

namespace VVRace
{
    // 거대 엘텍스 바늘(ApocryphaNeedle) 그리기: 바늘 몸체(엘텍스 바늘 그림을 크게) + 은은한 후광, 조준 중의 발사 직선 경고 띠(VV_NeedleLane 셰이더).
    // 머티리얼은 시작 시 한 번만 만들고, 바늘·띠마다 다른 값(투명도·진행도·길이)은 MaterialPropertyBlock으로 넘긴다.
    [StaticConstructorOnStartup]
    internal static class ApocryphaNeedleRenderer
    {
        private const string NeedleTexPath = "Things/Item/Equipment/VV_NeedleProjectile";

        // 바늘 그림 한 장의 크기(칸). 그림 높이의 73%가 바늘이라 바늘 길이는 약 4칸이다.
        private const float NeedleDrawSize = 5.5f;
        private static readonly Vector2 AuraDrawSize = new Vector2(1.6f, 4.6f);

        private static readonly Color BodyColor = Color.white;
        private static readonly Color GlowColor = new Color(1f, 0.82f, 0.6f, 0.55f);
        private static readonly Color AuraColor = new Color(1f, 0.7f, 0.45f, 0.35f);
        private static readonly Color LaneColor = new Color(1f, 0.42f, 0.5f, 1f);
        private static readonly Color LaneColorBossInside = new Color(1f, 0.82f, 0.35f, 1f);

        private static readonly Material BodyMat = MaterialPool.MatFrom(NeedleTexPath, ShaderDatabase.Transparent);
        private static readonly Material GlowMat = MaterialPool.MatFrom(NeedleTexPath, ShaderDatabase.MoteGlow);
        private static readonly Material AuraMat = MaterialPool.MatFrom("VFX/VV_ScentAura", ShaderDatabase.MoteGlow);
        private static readonly Material LaneMat = MaterialPool.MatFrom(BaseContent.WhiteTex, ShaderDatabase.LoadShader("Map/VV_NeedleLane"), Color.white);

        private static readonly MaterialPropertyBlock PropertyBlock = new MaterialPropertyBlock();
        private static readonly int ProgressPropertyId = Shader.PropertyToID("_Progress");
        private static readonly int LengthPropertyId = Shader.PropertyToID("_Length");
        private static readonly int WidthPropertyId = Shader.PropertyToID("_Width");

        // 같은 셰이더 안에서 뒤의 그림이 위에 오도록 겹쳐 그리는 높이 차이
        private const float LayerAltitudeStep = 0.002f;

        // 게임 시간이 매우 커지면 셰이더 안 무늬 좌표의 float 정밀도가 떨어지므로 1시간(실시간 1배속 기준)마다 되감는다.
        private const int AgeWrapTicks = 216000;

        // glow: 0(떠 있음) ~ 1(발사·귀환 중). 조준이 진행될수록 바늘이 달아오른다.
        public static void DrawNeedle(Vector3 pos, float angle, float alpha, float glow)
        {
            var rotation = Quaternion.AngleAxis(angle, Vector3.up);

            var auraColor = AuraColor;
            auraColor.a *= alpha * (0.6f + 0.6f * glow);
            DrawQuad(AuraMat, pos, rotation, AuraDrawSize, auraColor);

            pos.y += LayerAltitudeStep;
            var bodyColor = BodyColor;
            bodyColor.a *= alpha;
            DrawQuad(BodyMat, pos, rotation, new Vector2(NeedleDrawSize, NeedleDrawSize), bodyColor);

            pos.y += LayerAltitudeStep;
            var glowColor = GlowColor;
            glowColor.a *= alpha * (0.5f + 0.5f * glow);
            DrawQuad(GlowMat, pos, rotation, new Vector2(NeedleDrawSize, NeedleDrawSize), glowColor);
        }

        // 발사 직선 전체(start에서 direction으로 length칸, 폭 width칸)를 경고 띠로 그린다. progress는 조준 진행도(0~1).
        // bossInside: 2페이즈에서 보스가 띠 안에 있음(이대로 쏘면 보스에게 꽂힌다) → 금빛 띠.
        public static void DrawLane(Vector3 start, Vector3 direction, float length, float width, float progress, float alpha, bool bossInside = false)
        {
            if (length < 0.1f || width <= 0f) { return; }

            // 메시 로컬 X축(셰이더 U)을 발사 방향으로 돌린다. (SoulLinkRenderer와 같은 규칙)
            var angle = Mathf.Atan2(-direction.z, direction.x) * Mathf.Rad2Deg;
            var center = start + direction * (length * 0.5f);
            center.y = AltitudeLayer.MoteLow.AltitudeFor();

            var matrix = Matrix4x4.TRS(center, Quaternion.AngleAxis(angle, Vector3.up), new Vector3(length, 1f, width));

            var color = bossInside ? LaneColorBossInside : LaneColor;
            color.a *= alpha;

            PropertyBlock.Clear();
            PropertyBlock.SetFloat(ShaderPropertyIDs.AgeSecs, (Find.TickManager.TicksGame % AgeWrapTicks) / 60f);
            PropertyBlock.SetColor(ShaderPropertyIDs.Color, color);
            PropertyBlock.SetFloat(ProgressPropertyId, progress);
            PropertyBlock.SetFloat(LengthPropertyId, length);
            PropertyBlock.SetFloat(WidthPropertyId, width);

            Graphics.DrawMesh(MeshPool.plane10, matrix, LaneMat, 0, null, 0, PropertyBlock);
        }

        private static void DrawQuad(Material mat, Vector3 pos, Quaternion rotation, Vector2 size, Color color)
        {
            if (color.a <= 0f) { return; }

            var matrix = Matrix4x4.TRS(pos, rotation, new Vector3(size.x, 1f, size.y));

            PropertyBlock.Clear();
            PropertyBlock.SetColor(ShaderPropertyIDs.Color, color);
            Graphics.DrawMesh(MeshPool.plane10, matrix, mat, 0, null, 0, PropertyBlock);
        }
    }
}
