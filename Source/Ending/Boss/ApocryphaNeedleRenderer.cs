using RimWorld;
using UnityEngine;
using Verse;

namespace VVRace
{
    [StaticConstructorOnStartup]
    internal static class ApocryphaNeedleRenderer
    {
        private const string NeedleTexPath = "Things/Item/Equipment/VV_NeedleProjectile";

        // 그림 크기는 5.5칸이며 바늘이 높이의 73%를 차지해 실제 길이는 약 4칸이다.
        private const float NeedleDrawSize = 5.5f;
        private static readonly Vector2 AuraDrawSize = new Vector2(1.6f, 4.6f);

        private static readonly Color BodyColor = Color.white;
        private static readonly Color GlowColor = new Color(1f, 0.82f, 0.6f, 0.55f);
        private static readonly Color AuraColor = new Color(1f, 0.7f, 0.45f, 0.35f);
        private static readonly Color LaneColor = new Color(1f, 0.84f, 0.45f, 1f);
        private static readonly Color LaneCoreColor = new Color(1f, 0.94f, 0.75f, 1f);
        private static readonly Color LaneColorBossInside = new Color(1f, 0.36f, 0.82f, 1f);
        private static readonly Color LaneCoreColorBossInside = new Color(1f, 0.8f, 0.95f, 1f);

        // 마법진 바깥 고리(VV_MagicCircle r=0.95)가 경고 띠 테두리선(VV_NeedleLane 가장자리에서 0.07칸 안쪽)에 맞닿도록 띠 폭으로 크기를 정한다.
        private const float LotusRingRadius = 0.95f;
        private const float LaneEdgeInset = 0.07f;

        private const float PlantedMarkerDrawSize = 1.8f;
        private const float PlantedMarkerBloomSecs = 0.8f;
        private const float PlantedMarkerPulseSpeed = 2f;

        private const string StuckTexPath = "VFX/VV_GiantNeedleStuck";
        private const string StuckGlowTexPath = "VFX/VV_GiantNeedleStuckGlow";
        private static readonly Vector2 StuckDrawSize = new Vector2(2.75f, NeedleDrawSize);
        private static readonly Color CrackGlowColor = new Color(1f, 0.8f, 0.5f, 0.85f);

        private const float CrackFlareSecs = 1.2f;
        private const float CrackPulseSpeed = 2.4f;

        private static readonly Material BodyMat = MaterialPool.MatFrom(NeedleTexPath, ShaderDatabase.Transparent);
        private static readonly Material GlowMat = MaterialPool.MatFrom(NeedleTexPath, ShaderDatabase.MoteGlow);
        private static readonly Material AuraMat = MaterialPool.MatFrom("VFX/VV_ScentAura", ShaderDatabase.MoteGlow);
        private static readonly Material StuckBodyMat = MaterialPool.MatFrom(StuckTexPath, ShaderDatabase.Transparent);
        private static readonly Material StuckGlowMat = MaterialPool.MatFrom(StuckTexPath, ShaderDatabase.MoteGlow);
        private static readonly Material CrackGlowMat = MaterialPool.MatFrom(StuckGlowTexPath, ShaderDatabase.MoteGlow);
        private static readonly Material LaneMat = MaterialPool.MatFrom(BaseContent.WhiteTex, ShaderDatabase.LoadShader("Map/VV_NeedleLane"), Color.white);
        private static readonly Material WarpMat = MakeWarpMaterial();

        // 최대 반폭 2.8칸까지 퍼지는 왜곡을 덮도록 폭 3칸의 경고 띠보다 넓게 그린다.
        private const float WarpDrawWidth = 7f;
        private static readonly Color WarpColor = new Color(1f, 0.95f, 0.85f, 1f);

        private static Material MakeWarpMaterial()
        {
            var line = ContentFinder<Texture2D>.Get("Things/Mote/PsycastPsychicLine", reportFailure: false) ?? BaseContent.ClearTex;
            var material = MaterialPool.MatFrom(line, ShaderDatabase.LoadShader("Map/VV_NeedleWarp"), Color.white);
            material.SetTexture("_DistortionTex", ContentFinder<Texture2D>.Get("Things/Mote/PsycastPsychicLineMask", reportFailure: false) ?? BaseContent.BlackTex);
            material.SetTexture("_NoiseTex", ContentFinder<Texture2D>.Get("Things/Mote/PsycastNoise", reportFailure: false) ?? BaseContent.BlackTex);
            return material;
        }
        private static readonly Material LotusMat = MakeLotusMaterial();

        private static readonly MaterialPropertyBlock PropertyBlock = new MaterialPropertyBlock();
        private static readonly int ProgressPropertyId = Shader.PropertyToID("_Progress");
        private static readonly int LengthPropertyId = Shader.PropertyToID("_Length");
        private static readonly int WidthPropertyId = Shader.PropertyToID("_Width");
        private static readonly int CoreColorPropertyId = Shader.PropertyToID("_CoreColor");
        private static readonly int DurationPropertyId = Shader.PropertyToID("_Duration");
        private static readonly int SeedPropertyId = Shader.PropertyToID("_RandomPerObjectOffsetRandom");
        private static readonly int FrontPropertyId = Shader.PropertyToID("_Front");

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

        // 같은 셰이더에서 나중에 그린 그림이 위에 오도록 적용하는 높이 차이.
        private const float LayerAltitudeStep = 0.002f;

        // 셰이더 무늬 좌표의 float 정밀도를 유지하도록 1배속 기준 1시간마다 시간을 되감는다.
        private const int AgeWrapTicks = 216000;

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

        public static void DrawStuckNeedle(Vector3 pos, float angle, float alpha, float stuckSecs)
        {
            var rotation = Quaternion.AngleAxis(angle, Vector3.up);

            var bodyColor = BodyColor;
            bodyColor.a *= alpha;
            DrawQuad(StuckBodyMat, pos, rotation, StuckDrawSize, bodyColor);

            pos.y += LayerAltitudeStep;
            var glowColor = GlowColor;
            glowColor.a *= alpha * 0.5f;
            DrawQuad(StuckGlowMat, pos, rotation, StuckDrawSize, glowColor);

            pos.y += LayerAltitudeStep;
            var flare = Mathf.Clamp01(1f - stuckSecs / CrackFlareSecs);
            var pulse = 0.6f + 0.25f * Mathf.Sin(stuckSecs * CrackPulseSpeed);
            var crackColor = CrackGlowColor;
            crackColor.a *= alpha * Mathf.Lerp(pulse, 1f, flare * flare);
            DrawQuad(CrackGlowMat, pos, rotation, StuckDrawSize, crackColor);
        }

        public static void DrawLane(Vector3 start, Vector3 direction, float length, float width, float progress, float alpha, bool bossInside, float aimSecs, float aimDuration, int seed)
        {
            if (length < 0.1f || width <= 0f) { return; }

            // SoulLinkRenderer와 같이 메시의 로컬 X축(셰이더 U)을 발사 방향에 맞춘다.
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

            var lotusPos = start;
            lotusPos.y = AltitudeLayer.MoteLow.AltitudeFor() + LayerAltitudeStep;
            var lotusSize = (width * 0.5f - LaneEdgeInset) * 2f / LotusRingRadius;
            PropertyBlock.Clear();
            PropertyBlock.SetFloat(ShaderPropertyIDs.AgeSecs, aimSecs);
            PropertyBlock.SetFloat(DurationPropertyId, Mathf.Max(0.1f, aimDuration));
            PropertyBlock.SetFloat(SeedPropertyId, seed);
            PropertyBlock.SetColor(ShaderPropertyIDs.Color, color);
            PropertyBlock.SetColor(CoreColorPropertyId, coreColor);
            Graphics.DrawMesh(MeshPool.plane10, Matrix4x4.TRS(lotusPos, Quaternion.identity, new Vector3(lotusSize, 1f, lotusSize)), LotusMat, 0, null, 0, PropertyBlock);
        }

        public static void DrawPlantedMarker(Vector3 pos, float ageSecs, float alpha, int seed)
        {
            var pulse = 0.55f + 0.2f * Mathf.Sin(ageSecs * PlantedMarkerPulseSpeed);
            var color = LaneColor;
            color.a *= alpha * pulse;

            pos.y = AltitudeLayer.MoteLow.AltitudeFor();
            PropertyBlock.Clear();
            PropertyBlock.SetFloat(ShaderPropertyIDs.AgeSecs, ageSecs);
            PropertyBlock.SetFloat(DurationPropertyId, PlantedMarkerBloomSecs);
            PropertyBlock.SetFloat(SeedPropertyId, seed);
            PropertyBlock.SetColor(ShaderPropertyIDs.Color, color);
            PropertyBlock.SetColor(CoreColorPropertyId, LaneCoreColor);
            Graphics.DrawMesh(MeshPool.plane10, Matrix4x4.TRS(pos, Quaternion.identity, new Vector3(PlantedMarkerDrawSize, 1f, PlantedMarkerDrawSize)), LotusMat, 0, null, 0, PropertyBlock);
        }

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
