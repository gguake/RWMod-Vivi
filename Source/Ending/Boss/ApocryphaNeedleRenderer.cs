using RimWorld;
using UnityEngine;
using Verse;

namespace VVRace
{
    // 거대 엘텍스 바늘(ApocryphaNeedle) 그리기: 바늘 몸체(엘텍스 바늘 그림을 크게) + 은은한 후광, 조준 중의 발사 직선 경고 띠(VV_NeedleLane 셰이더)와
    // 발사 지점의 작은 연꽃(요정 비비 사출탄의 시전 연꽃과 같은 VV_MagicCircle 셰이더, 조준 시간 동안 피어난다).
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
        // 띠: 요정 비비 사출탄 조준 연꽃과 같은 금빛(2페이즈에서 보스가 띠 안에 있으면 자홍빛 — 이대로 쏘면 보스에게 꽂힌다)
        private static readonly Color LaneColor = new Color(1f, 0.84f, 0.45f, 1f);
        private static readonly Color LaneCoreColor = new Color(1f, 0.94f, 0.75f, 1f);
        private static readonly Color LaneColorBossInside = new Color(1f, 0.36f, 0.82f, 1f);
        private static readonly Color LaneCoreColorBossInside = new Color(1f, 0.8f, 0.95f, 1f);

        // 발사 지점 연꽃(바늘이 떠 있는 띠 시작점, 크기 LotusDrawSize칸)
        private const float LotusDrawSize = 2.6f;

        private static readonly Material BodyMat = MaterialPool.MatFrom(NeedleTexPath, ShaderDatabase.Transparent);
        private static readonly Material GlowMat = MaterialPool.MatFrom(NeedleTexPath, ShaderDatabase.MoteGlow);
        private static readonly Material AuraMat = MaterialPool.MatFrom("VFX/VV_ScentAura", ShaderDatabase.MoteGlow);
        private static readonly Material LaneMat = MaterialPool.MatFrom(BaseContent.WhiteTex, ShaderDatabase.LoadShader("Map/VV_NeedleLane"), Color.white);
        private static readonly Material WarpMat = MaterialPool.MatFrom(BaseContent.WhiteTex, ShaderDatabase.LoadShader("Map/VV_NeedleWarp"), Color.white);

        // 발사 왜곡: 양옆으로 퍼지는 파면(최대 약 2.7칸)까지 덮도록 경고 띠(3칸)보다 넓게 그린다.
        private const float WarpDrawWidth = 7f;
        private static readonly Color WarpColor = new Color(1f, 0.9f, 0.7f, 1f);
        private static readonly Material LotusMat = MakeLotusMaterial();

        private static readonly MaterialPropertyBlock PropertyBlock = new MaterialPropertyBlock();
        private static readonly int ProgressPropertyId = Shader.PropertyToID("_Progress");
        private static readonly int LengthPropertyId = Shader.PropertyToID("_Length");
        private static readonly int WidthPropertyId = Shader.PropertyToID("_Width");
        private static readonly int CoreColorPropertyId = Shader.PropertyToID("_CoreColor");
        private static readonly int DurationPropertyId = Shader.PropertyToID("_Duration");
        private static readonly int SeedPropertyId = Shader.PropertyToID("_RandomPerObjectOffsetRandom");
        private static readonly int FrontPropertyId = Shader.PropertyToID("_Front");

        // 사출탄 시전 연꽃(VV_Mote_ApocryphaAimCharge)과 같은 값: 바깥 고리 없이 꽃잎 6장이 피어나며 중심이 밝아진다.
        private static Material MakeLotusMaterial()
        {
            var material = MaterialPool.MatFrom(BaseContent.WhiteTex, ShaderDatabase.LoadShader("Map/VV_MagicCircle"), Color.white);
            material.SetFloat("_PetalCount", 6f);
            material.SetFloat("_RingAlpha", 0.45f);
            material.SetFloat("_GuideAlpha", 0.6f);
            material.SetFloat("_CoreGlow", 1f);
            material.SetFloat("_FadeInSecs", 0.25f);
            material.SetFloat("_LineScale", 1.8f);
            material.SetFloat("_Brightness", 0.85f);
            return material;
        }

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

        // 발사 직선 전체(start에서 direction으로 length칸, 폭 width칸)를 경고 띠로 그리고, 시작점에 연꽃을 피운다. progress는 조준 진행도(0~1),
        // aimSecs/aimDuration은 조준 경과·전체 시간(초, 연꽃이 피는 정도), seed는 연꽃 방향(바늘마다 다르게).
        // bossInside: 2페이즈에서 보스가 띠 안에 있음(이대로 쏘면 보스에게 꽂힌다) → 자홍빛.
        public static void DrawLane(Vector3 start, Vector3 direction, float length, float width, float progress, float alpha, bool bossInside, float aimSecs, float aimDuration, int seed)
        {
            if (length < 0.1f || width <= 0f) { return; }

            // 메시 로컬 X축(셰이더 U)을 발사 방향으로 돌린다. (SoulLinkRenderer와 같은 규칙)
            var angle = Mathf.Atan2(-direction.z, direction.x) * Mathf.Rad2Deg;
            var center = start + direction * (length * 0.5f);
            center.y = AltitudeLayer.MoteLow.AltitudeFor();

            var matrix = Matrix4x4.TRS(center, Quaternion.AngleAxis(angle, Vector3.up), new Vector3(length, 1f, width));

            var color = bossInside ? LaneColorBossInside : LaneColor;
            color.a *= alpha;
            var coreColor = bossInside ? LaneCoreColorBossInside : LaneCoreColor;

            PropertyBlock.Clear();
            PropertyBlock.SetFloat(ShaderPropertyIDs.AgeSecs, (Find.TickManager.TicksGame % AgeWrapTicks) / 60f);
            PropertyBlock.SetColor(ShaderPropertyIDs.Color, color);
            PropertyBlock.SetColor(CoreColorPropertyId, coreColor);
            PropertyBlock.SetFloat(ProgressPropertyId, progress);
            PropertyBlock.SetFloat(LengthPropertyId, length);
            PropertyBlock.SetFloat(WidthPropertyId, width);

            Graphics.DrawMesh(MeshPool.plane10, matrix, LaneMat, 0, null, 0, PropertyBlock);

            // 발사 지점 연꽃: 띠보다 살짝 위에, 조준 시간 동안 피어난다.
            var lotusPos = start;
            lotusPos.y = AltitudeLayer.MoteLow.AltitudeFor() + LayerAltitudeStep;
            PropertyBlock.Clear();
            PropertyBlock.SetFloat(ShaderPropertyIDs.AgeSecs, aimSecs);
            PropertyBlock.SetFloat(DurationPropertyId, Mathf.Max(0.1f, aimDuration));
            PropertyBlock.SetFloat(SeedPropertyId, seed);
            PropertyBlock.SetColor(ShaderPropertyIDs.Color, color);
            PropertyBlock.SetColor(CoreColorPropertyId, coreColor);
            Graphics.DrawMesh(MeshPool.plane10, Matrix4x4.TRS(lotusPos, Quaternion.identity, new Vector3(LotusDrawSize, 1f, LotusDrawSize)), LotusMat, 0, null, 0, PropertyBlock);
        }

        // 발사 직후 촉이 지나간 직선(start에서 direction으로 front칸까지, 쿼드는 length칸 전체) 위의 공간이 순간 일그러진다(VV_NeedleWarp 셰이더).
        // ageSecs: 발사 뒤 경과(초). 셰이더가 _Duration(0.55초) 동안 퍼지며 잦아들게 한다.
        public static void DrawWarp(Vector3 start, Vector3 direction, float length, float front, float ageSecs, float alpha)
        {
            if (length < 0.1f || alpha <= 0f) { return; }

            var angle = Mathf.Atan2(-direction.z, direction.x) * Mathf.Rad2Deg;
            var center = start + direction * (length * 0.5f);
            center.y = AltitudeLayer.MoteOverhead.AltitudeFor();
            var matrix = Matrix4x4.TRS(center, Quaternion.AngleAxis(angle, Vector3.up), new Vector3(length, 1f, WarpDrawWidth));

            var color = WarpColor;
            color.a *= alpha;

            PropertyBlock.Clear();
            PropertyBlock.SetFloat(ShaderPropertyIDs.AgeSecs, ageSecs);
            PropertyBlock.SetColor(ShaderPropertyIDs.Color, color);
            PropertyBlock.SetFloat(LengthPropertyId, length);
            PropertyBlock.SetFloat(WidthPropertyId, WarpDrawWidth);
            PropertyBlock.SetFloat(FrontPropertyId, front);
            Graphics.DrawMesh(MeshPool.plane10, matrix, WarpMat, 0, null, 0, PropertyBlock);
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
